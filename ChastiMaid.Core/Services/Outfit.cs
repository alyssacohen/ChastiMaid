using ChastiMaid.Core.Models;
using LiteDB;
using System.Collections.Generic;
using System.Linq;

namespace ChastiMaid.Core.Models;

public class Outfit
{
    public List<OutfitItem> Items { get; } = new();

    // FIXED: Dictionary now uses ObjectId to match the Model
    public Dictionary<ObjectId, string> ItemLayers { get; } = new();

    // Tracks occupied slots to prevent conflicts
    private HashSet<string> OccupiedSlots { get; } = new();

    public Outfit(List<OutfitItem> items = null)
    {
        if (items != null)
        {
            foreach (var item in items)
            {
                // Note: when reconstructing, we assume items are valid
                Items.Add(item);
                foreach (var slot in item.Slots) OccupiedSlots.Add(slot);
                // We pick the first layer as the primary for tracking
                ItemLayers[item.Id] = item.Layers.FirstOrDefault() ?? "base";
            }
        }
    }

    /// <summary>
    /// Calculates the total multiplier for the entire outfit.
    /// This can be used as a bonus/malus for other app functions.
    /// </summary>
    public double GetTotalMultiplier()
    {
        if (!Items.Any()) return 1.0;

        // Example: Product of all multipliers
        // If you prefer sum, change Aggregate to Sum
        return Items.Aggregate(1.0, (acc, item) => acc * item.Multiplier);
    }

    public bool CanAddOnLayer(OutfitItem item, string layer)
    {
        // 1. Check if any slot is already taken
        if (item.Slots.Any(slot => OccupiedSlots.Contains(slot)))
            return false;

        // 2. Check if the item actually supports this layer
        if (!item.Layers.Contains(layer))
            return false;

        return true;
    }

    public void AddItem(OutfitItem item, string layer)
    {
        Items.Add(item);
        ItemLayers[item.Id] = layer;
        foreach (var slot in item.Slots)
        {
            OccupiedSlots.Add(slot);
        }
    }
}