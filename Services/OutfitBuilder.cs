using ChastiMaid.Models;
using ChastiMaid.Repositories;

namespace ChastiMaid.Services;

/// <summary>
///     Builds a coherent Outfit from available clothing items and rules.
///     Ported from the Python OutfitBuilder prototype.
/// </summary>
public class OutfitBuilder
{
    private const int MaxFillAttemptsPerLayer = 10;

    private static readonly Dictionary<int, double> DefaultLayerProbability =
        WardrobeLayer.All.ToDictionary(l => l.Order, l => l.Probability);

    private readonly List<ClothingItem> _allItems;
    private readonly Dictionary<int, double> _layerProbability;
    private readonly List<ClothingItem> _pickingList = [];
    private readonly Random _random;
    private readonly Dictionary<int, (HashSet<int> Requires, HashSet<int> Incompatible)> _rules;

    private readonly List<ClothingItem> _seedItems = [];

    public OutfitBuilder(ProfileRepository repo, int? randomSeed = null)
        : this(repo.GetWardrobe(), repo.GetOutfitRules(), randomSeed)
    {
    }

    public OutfitBuilder(List<ClothingItem> items, List<OutfitRule> rules, int? randomSeed = null)
    {
        _allItems = items;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        _layerProbability = DefaultLayerProbability;

        _rules = [];
        foreach (var rule in rules)
        {
            Ensure(rule.ItemId);
            if (rule.Relation == OutfitRelation.Requires)
            {
                _rules[rule.ItemId].Requires.Add(rule.RelatedItemId);
            }
            else if (rule.Relation == OutfitRelation.Incompatible)
            {
                // Store both directions so candidate filtering works regardless of rule direction
                _rules[rule.ItemId].Incompatible.Add(rule.RelatedItemId);
                Ensure(rule.RelatedItemId);
                _rules[rule.RelatedItemId].Incompatible.Add(rule.ItemId);
            }
        }

        void Ensure(int id)
        {
            if (!_rules.ContainsKey(id))
            {
                _rules[id] = (new HashSet<int>(), new HashSet<int>());
            }
        }
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

        var remaining = _allItems
            .Where(i => !_pickingList.Contains(i))
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
            for (var attempt = 0; attempt < MaxFillAttemptsPerLayer; attempt++)
            {
                var placedIds = outfit.Items.Select(i => i.Id).ToHashSet();
                var candidates = remaining.Values
                    .Where(i => i.Layers.Contains(layer) && outfit.CanAddOnLayer(i, layer))
                    .Where(i => !_rules.TryGetValue(i.Id, out var r) || !r.Incompatible.Any(placedIds.Contains))
                    .ToList();
                if (candidates.Count == 0)
                {
                    break;
                }

                var chosen = WeightedChoice(candidates);
                outfit.AddItemOnLayer(chosen, layer);
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

        return outfit;
    }

    private void ResolveRequiredRules()
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var item in _pickingList.ToList())
            {
                if (!_rules.TryGetValue(item.Id, out var rule) || rule.Requires.Count == 0)
                {
                    continue;
                }

                if (_pickingList.Any(i => rule.Requires.Contains(i.Id)))
                {
                    continue;
                }

                var required = _allItems.Where(i => rule.Requires.Contains(i.Id)).ToList();
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
