using ChastiMaid.Core.Models;

namespace ChastiMaid.Core.Services
{
    /// <summary>
    /// Port of the Python OutfitBuilder. Generates coherent outfits
    /// based on layers, slots, and compatibility rules.
    /// </summary>
    public class OutfitGenerator
    {
        private readonly List<OutfitItem> _allItems;
        private readonly List<OutfitRule> _allRules;
        private readonly Random _random;

        // Layers configuration
        private readonly List<string> _layersOrder = new() { "undies", "base", "extra", "outer", "gears" };

        private readonly Dictionary<string, double> _layerProbability = new()
        {
            { "undies", 1.0 }, { "base", 1.0 }, { "extra", 0.7 }, { "outer", 1.0 }, { "gears", 0.5 }
        };

        public OutfitGenerator(IEnumerable<OutfitItem> items, IEnumerable<OutfitRule> rules, int? seed = null)
        {
            _allItems = items.ToList();
            _allRules = rules.ToList();
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Main build algorithm.
        /// </summary>
        public Outfit Generate(List<OutfitItem> seedItems)
        {
            var pickingList = ResolveRequirements(seedItems);

            // Note: Outfit constructor assumes items are valid, but here we build step-by-step
            var outfit = new Outfit();

            var unplacedSeeds = new List<OutfitItem>(pickingList);
            var remainingItems = _allItems.Except(pickingList).ToList();

            foreach (var layer in _layersOrder)
            {
                // Roll for layer probability (skip layer if roll fails)
                if (_random.NextDouble() > _layerProbability.GetValueOrDefault(layer, 1.0))
                    continue;

                // 1. Try to place seed items first
                // We use ToList() to allow modification of the collection during iteration if needed
                foreach (var seedItem in unplacedSeeds.Where(s => s.Layers.Contains(layer)).ToList())
                {
                    // FIX: Separated CanAdd check from Add action to match Outfit.cs signature
                    if (outfit.CanAddOnLayer(seedItem, layer))
                    {
                        outfit.AddItem(seedItem, layer);
                        unplacedSeeds.Remove(seedItem);
                    }
                }

                // 2. Fill remaining slots in this layer randomly (Weighted)
                FillLayerRandomly(outfit, layer, remainingItems);
            }

            return outfit;
        }

        /// <summary>
        /// Expands the 'requires' chain logic.
        /// </summary>
        private List<OutfitItem> ResolveRequirements(List<OutfitItem> seeds)
        {
            var result = new HashSet<OutfitItem>(seeds);
            bool changed = true;

            while (changed)
            {
                changed = false;
                var currentBatch = result.ToList();
                foreach (var item in currentBatch)
                {
                    // Ensure OutfitRule uses ObjectId for comparisons
                    var requirements = _allRules
                        .Where(r => r.ItemId == item.Id && r.Relation == "requires")
                        .Select(r => r.RelatedItemId);

                    foreach (var reqId in requirements)
                    {
                        if (!result.Any(i => i.Id == reqId))
                        {
                            var candidate = _allItems.FirstOrDefault(i => i.Id == reqId);
                            if (candidate != null && result.Add(candidate))
                                changed = true;
                        }
                    }
                }
            }
            return result.ToList();
        }

        private void FillLayerRandomly(Outfit outfit, string layer, List<OutfitItem> pool)
        {
            // Limit attempts to fill slots to avoid infinite loops
            for (int i = 0; i < 5; i++)
            {
                var candidates = pool
                    .Where(it => it.Layers.Contains(layer) &&
                                 outfit.CanAddOnLayer(it, layer) &&
                                 IsCompatibleWithOutfit(it, outfit)) // Logic check
                    .ToList();

                if (!candidates.Any()) break;

                var chosen = WeightedChoice(candidates);

                // Since we already checked CanAddOnLayer in the LINQ query, we can safely add
                outfit.AddItem(chosen, layer);
                pool.Remove(chosen);
            }
        }

        /// <summary>
        /// Checks if an item is compatible with the current outfit based on global rules.
        /// </summary>
        private bool IsCompatibleWithOutfit(OutfitItem candidate, Outfit outfit)
        {
            // 1. Check Incompatibilities
            bool hasIncompatibility = _allRules.Any(r =>
                r.Relation == "incompatible" &&
                ((r.ItemId == candidate.Id && outfit.Items.Any(oi => oi.Id == r.RelatedItemId)) ||
                 (r.RelatedItemId == candidate.Id && outfit.Items.Any(oi => oi.Id == r.ItemId))));

            if (hasIncompatibility) return false;

            // 2. Check Requirements
            // For random filling, if a candidate requires an item NOT yet present, we skip it.
            // (Simplification: we don't auto-add requirements during random fill to avoid recursion hell)
            var requirements = _allRules.Where(r => r.ItemId == candidate.Id && r.Relation == "requires");
            foreach (var req in requirements)
            {
                if (!outfit.Items.Any(oi => oi.Id == req.RelatedItemId))
                    return false;
            }

            return true;
        }

        private OutfitItem WeightedChoice(List<OutfitItem> candidates)
        {
            double totalWeight = candidates.Sum(c => c.Weight);
            double r = _random.NextDouble() * totalWeight;
            double sum = 0;

            foreach (var item in candidates)
            {
                sum += item.Weight;
                if (r <= sum) return item;
            }
            return candidates.Last();
        }
    }
}
