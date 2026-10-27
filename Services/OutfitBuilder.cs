using ChastiMaid.Models;
using ChastiMaid.Repositories;

namespace ChastiMaid.Services;

/// <summary>
///     Builds a coherent Outfit from available clothing items and rules.
///     Ported from the Python OutfitBuilder prototype.
/// </summary>
public class OutfitBuilder
{
    private readonly List<ClothingItem> _allItems;
    private readonly OutfitRuleEngine _engine;
    private readonly IReadOnlyDictionary<int, double> _layerProbability;
    private readonly List<ClothingItem> _pickingList = [];
    private readonly Random _random;

    private readonly List<ClothingItem> _seedItems = [];

    public OutfitBuilder(ProfileRepository repo, int? randomSeed = null)
        : this(repo.GetWardrobe(), repo.GetOutfitRules(), randomSeed)
    {
    }

    public OutfitBuilder(List<ClothingItem> items, List<OutfitRule> rules, int? randomSeed = null)
    {
        _allItems = items;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        _layerProbability = WardrobeBalance.LayerProbability;
        _engine = new OutfitRuleEngine(rules);
    }

    public void AddSeedItems(IEnumerable<ClothingItem> items)
    {
        foreach (var item in items)
        {
            if (!_seedItems.Contains(item))
            {
                _seedItems.Add(item);
            }

            if (!_pickingList.Contains(item))
            {
                _pickingList.Add(item);
            }
        }
    }

    public Outfit Build()
    {
        ResolveRequiredRules();

        // Weight 0 = never a random pick: such items only enter as seeds (forced outfits, Plug & Play).
        var remaining = _allItems
            .Where(i => !_pickingList.Contains(i) && i.Weight > 0)
            .ToDictionary(i => i.Id);

        var unplaced = new List<ClothingItem>(_pickingList);
        var outfit = new Outfit();

        foreach (var layer in Outfit.LayersOrder)
        {
            var prob = _layerProbability.GetValueOrDefault(layer, 1.0);
            if (_random.NextDouble() >= prob)
            {
                continue;
            }

            // Place seed items on this layer
            foreach (var seed in unplaced.ToList())
            {
                if (!seed.Layers.Contains(layer))
                {
                    continue;
                }

                if (outfit.AddItemOnLayer(seed, layer))
                {
                    unplaced.Remove(seed);
                }
            }

            // Fill layer with random candidates (exclude items incompatible with already-placed items)
            for (var attempt = 0; attempt < WardrobeBalance.MaxFillAttemptsPerLayer; attempt++)
            {
                var candidates = remaining.Values
                    .Where(i => i.Layers.Contains(layer) && outfit.CanAddOnLayer(i, layer))
                    .Where(i => !_engine.ConflictsWithAny(i, outfit.Items))
                    .ToList();
                if (candidates.Count == 0)
                {
                    break;
                }

                // If chosen has unmet "requires", this also tries to place them in free slots.
                // If they don't fit anywhere, the filler itself is dropped instead of being
                // added with a violated requires rule.
                var chosen = WeightedChoice(candidates);
                TryAddWithRequires(outfit, remaining, chosen, layer);
                remaining.Remove(chosen.Id);
            }
        }

        // Fallback for unplaced seeds: replace non-seed items on conflict
        foreach (var seed in unplaced.ToList())
        {
            foreach (var layer in Outfit.LayersOrder.Where(l => seed.Layers.Contains(l)))
            {
                if (outfit.AddItemOnLayer(seed, layer))
                {
                    unplaced.Remove(seed);
                    break;
                }

                var occupying = outfit.ItemsInLayer(layer, seed.Slots).ToList();
                var replaceable = occupying.FirstOrDefault(i => !_seedItems.Contains(i));
                if (replaceable is null)
                {
                    continue;
                }

                outfit.RemoveItem(replaceable);
                if (outfit.AddItemOnLayer(seed, layer))
                {
                    unplaced.Remove(seed);
                    break;
                }
            }
        }

        // A seed arriving late on a layer can evict a filler that was holding a slot open for
        // someone else's required item (see TryPlaceOneRequired). Drop any filler left with an
        // unmet requires rule rather than leaving the violation in the final outfit.
        DropFillersWithUnmetRequires(outfit);

        return outfit;
    }

    private void DropFillersWithUnmetRequires(Outfit outfit)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var item in outfit.Items.ToList())
            {
                if (_seedItems.Contains(item) || _engine.RequiresSatisfied(item, outfit.Items))
                {
                    continue;
                }

                outfit.RemoveItem(item);
                changed = true;
            }
        } while (changed);
    }

    // Adds `item` on `layer`, and if it has an unmet "requires" rule, tries to also place one
    // of its required items in a free slot (recursively, in case that one has requires too).
    // If the requirement can't be satisfied anywhere, the whole placement is rolled back so the
    // filler is never added with a violated rule.
    private bool TryAddWithRequires(Outfit outfit, Dictionary<int, ClothingItem> remaining, ClothingItem item,
        int layer)
    {
        if (!outfit.AddItemOnLayer(item, layer))
        {
            return false;
        }

        if (_engine.RequiresSatisfied(item, outfit.Items))
        {
            return true;
        }

        if (TryPlaceOneRequired(outfit, remaining, item))
        {
            return true;
        }

        outfit.RemoveItem(item);
        return false;
    }

    // Tries each of item's required options, in weighted-random order, on every free slot they
    // fit. Never evicts an already-placed item — only uses genuinely free space.
    private bool TryPlaceOneRequired(Outfit outfit, Dictionary<int, ClothingItem> remaining, ClothingItem item)
    {
        var candidates = _engine.ItemsWithAnyTag(remaining.Values, _engine.RequiredTagsFor(item)).ToList();

        foreach (var candidate in WeightedOrder(candidates))
        {
            foreach (var candLayer in candidate.Layers)
            {
                if (!outfit.CanAddOnLayer(candidate, candLayer))
                {
                    continue;
                }

                remaining.Remove(candidate.Id);
                if (TryAddWithRequires(outfit, remaining, candidate, candLayer))
                {
                    return true;
                }

                remaining[candidate.Id] = candidate;
            }
        }

        return false;
    }

    // Returns items shuffled by weighted-random draw (higher weight = more likely first).
    private List<ClothingItem> WeightedOrder(List<ClothingItem> items)
    {
        var pool = new List<ClothingItem>(items);
        var ordered = new List<ClothingItem>();
        while (pool.Count > 0)
        {
            var pick = WeightedChoice(pool);
            ordered.Add(pick);
            pool.Remove(pick);
        }

        return ordered;
    }

    private void ResolveRequiredRules()
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var item in _pickingList.ToList())
            {
                var requiredTags = _engine.RequiredTagsFor(item);
                if (requiredTags.Count == 0)
                {
                    continue;
                }

                if (_engine.RequiresSatisfied(item, _pickingList))
                {
                    continue;
                }

                var required = _engine.ItemsWithAnyTag(_allItems, requiredTags).ToList();
                if (required.Count == 0)
                {
                    continue;
                }

                var picked = WeightedChoice(required);
                if (_pickingList.Contains(picked))
                {
                    continue;
                }

                _pickingList.Add(picked);
                changed = true;
            }
        } while (changed);
    }

    private ClothingItem WeightedChoice(List<ClothingItem> items)
    {
        var totalWeight = items.Sum(i => i.Weight);
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var item in items)
        {
            cumulative += item.Weight;
            if (roll <= cumulative)
            {
                return item;
            }
        }

        return items[^1];
    }
}
