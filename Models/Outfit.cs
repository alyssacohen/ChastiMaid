namespace ChastiMaid.Models;

/// <summary>
/// Represents a fully built outfit: items placed on specific layers with slot tracking.
/// </summary>
public class Outfit
{
    public static readonly List<string> LayersOrder = ["undies", "extra", "base", "outer", "gears"];

    private readonly List<ClothingItem> _items = [];
    private readonly Dictionary<int, string> _itemLayer = [];       // itemId -> layer
    private readonly Dictionary<string, HashSet<string>> _usedSlots = []; // layer -> occupied slots

    public IReadOnlyList<ClothingItem> Items => _items;

    public double TotalMultiplier => _items.Aggregate(1.0, (acc, i) => acc * i.Multiplier);

    public IEnumerable<ClothingItem> ItemsInLayer(string layer, IEnumerable<string>? slots = null)
    {
        var result = _items.Where(i => _itemLayer.GetValueOrDefault(i.Id) == layer);
        if (slots is not null)
        {
            var slotSet = slots.ToHashSet();
            result = result.Where(i => i.Slots.Any(slotSet.Contains));
        }
        return result;
    }

    public string? LayerOf(ClothingItem item) => _itemLayer.GetValueOrDefault(item.Id);

    public bool CanAddOnLayer(ClothingItem item, string layer)
    {
        if (!item.Layers.Contains(layer)) return false;
        var used = _usedSlots.GetValueOrDefault(layer) ?? [];
        return !item.Slots.Any(used.Contains);
    }

    public bool AddItemOnLayer(ClothingItem item, string layer)
    {
        if (!CanAddOnLayer(item, layer)) return false;

        if (_items.Contains(item))
        {
            var oldLayer = _itemLayer.GetValueOrDefault(item.Id);
            if (oldLayer == layer) return true;
            if (oldLayer is not null)
                foreach (var s in item.Slots) _usedSlots.GetValueOrDefault(oldLayer)?.Remove(s);
        }
        else
        {
            _items.Add(item);
        }

        _itemLayer[item.Id] = layer;
        if (!_usedSlots.ContainsKey(layer)) _usedSlots[layer] = [];
        foreach (var s in item.Slots) _usedSlots[layer].Add(s);
        return true;
    }

    public bool RemoveItem(ClothingItem item)
    {
        if (!_items.Contains(item)) return false;
        if (_itemLayer.TryGetValue(item.Id, out var layer))
        {
            foreach (var s in item.Slots) _usedSlots.GetValueOrDefault(layer)?.Remove(s);
            _itemLayer.Remove(item.Id);
        }
        _items.Remove(item);
        return true;
    }

    public override string ToString()
    {
        var lines = new List<string>();
        foreach (var layer in LayersOrder)
        {
            var layerItems = ItemsInLayer(layer).ToList();
            if (layerItems.Count == 0) continue;
            lines.Add($"{layer}:");
            foreach (var it in layerItems)
                lines.Add($"  • {it.Name} ({string.Join(", ", it.Slots.Order())})");
        }
        lines.Add($"Total Multiplier: x{TotalMultiplier:F2}");
        return string.Join("\n", lines);
    }
}
