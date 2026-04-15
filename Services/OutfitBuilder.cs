using ChastiMaid.Models;

namespace ChastiMaid.Services;

/// <summary>
/// Builds a coherent Outfit from available clothing items and rules.
/// Ported from the Python OutfitBuilder prototype.
/// </summary>
public class OutfitBuilder
{
    private static readonly Dictionary<string, double> DefaultLayerProbability = new()
    {
        ["undies"] = 1.0,
        ["base"]   = 1.0,
        ["extra"]  = 0.7,
        ["outer"]  = 1.0,
        ["gears"]  = 0.5,
    };

    private const int MaxFillAttemptsPerLayer = 10;
    private const double ReplacementThreshold = 0.8;

    private readonly List<ClothingItem> _allItems;
    private readonly Dictionary<int, (HashSet<int> Requires, HashSet<int> Incompatible)> _rules;
    private readonly Dictionary<string, double> _layerProbability;
    private readonly Random _random;

    private readonly List<ClothingItem> _seedItems = [];
    private readonly List<ClothingItem> _pickingList = [];

    public OutfitBuilder(ProfileRepository repo, int? randomSeed = null)
    {
        _allItems = repo.GetWardrobe();
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        _layerProbability = DefaultLayerProbability;

        _rules = [];
        foreach (var rule in repo.GetOutfitRules())
        {
            if (!_rules.ContainsKey(rule.ItemId))
                _rules[rule.ItemId] = (new HashSet<int>(), new HashSet<int>());

            if (rule.Relation == OutfitRelation.Requires)
                _rules[rule.ItemId].Requires.Add(rule.RelatedItemId);
            else if (rule.Relation == OutfitRelation.Incompatible)
                _rules[rule.ItemId].Incompatible.Add(rule.RelatedItemId);
        }
    }

    public void AddSeedItems(IEnumerable<ClothingItem> items)
    {
        foreach (var item in items)
        {
            if (!_seedItems.Contains(item)) _seedItems.Add(item);
            if (!_pickingList.Contains(item)) _pickingList.Add(item);
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
            if (_random.NextDouble() >= prob) continue;

            // Place seed items on this layer
            foreach (var seed in unplaced.ToList())
            {
                if (!seed.Layers.Contains(layer)) continue;
                if (outfit.AddItemOnLayer(seed, layer))
                    unplaced.Remove(seed);
            }

            // Fill layer with random candidates
            for (var attempt = 0; attempt < MaxFillAttemptsPerLayer; attempt++)
            {
                var candidates = remaining.Values
                    .Where(i => i.Layers.Contains(layer) && outfit.CanAddOnLayer(i, layer))
                    .ToList();
                if (candidates.Count == 0) break;

                var chosen = WeightedChoice(candidates);
                outfit.AddItemOnLayer(chosen, layer);
                remaining.Remove(chosen.Id);
            }
        }

        // Conservative fallback for unplaced seeds
        foreach (var seed in unplaced.ToList())
        {
            foreach (var layer in Outfit.LayersOrder.Where(l => seed.Layers.Contains(l)))
            {
                if (outfit.AddItemOnLayer(seed, layer))
                {
                    unplaced.Remove(seed);
                    break;
                }

                // Try replacing a low-weight non-seed item
                var occupying = outfit.ItemsInLayer(layer, seed.Slots).ToList();
                if (occupying.Count == 0) continue;

                var weakest = occupying.MinBy(i => i.Weight)!;
                if (weakest.Weight < seed.Weight * ReplacementThreshold && !_seedItems.Contains(weakest))
                {
                    outfit.RemoveItem(weakest);
                    outfit.AddItemOnLayer(seed, layer);
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
                if (!_rules.TryGetValue(item.Id, out var rule) || rule.Requires.Count == 0) continue;
                if (_pickingList.Any(i => rule.Requires.Contains(i.Id))) continue;

                var required = _allItems.Where(i => rule.Requires.Contains(i.Id)).ToList();
                if (required.Count == 0) continue;

                var picked = WeightedChoice(required);
                if (_pickingList.Contains(picked)) continue;
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
            if (roll <= cumulative) return item;
        }
        return items[^1];
    }
}
