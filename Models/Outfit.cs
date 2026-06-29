namespace ChastiMaid.Models;

/// <summary>
///     Represents a fully built outfit: items placed on specific layers with slot tracking.
/// </summary>
public class Outfit
{
    public static readonly IReadOnlyList<int> LayersOrder =
        WardrobeLayer.All.OrderBy(l => l.Order).Select(l => l.Order).ToList();

    private readonly Dictionary<int, int> _itemLayer = []; // itemId -> layer order

    private readonly List<ClothingItem> _items = [];
    private readonly Dictionary<int, HashSet<string>> _usedSlots = []; // layer order -> occupied slots

    public IReadOnlyList<ClothingItem> Items => _items;

    public double TotalMultiplier => _items.Aggregate(1.0, (acc, i) => acc * i.Multiplier);

    public IEnumerable<ClothingItem> ItemsInLayer(int layer, IEnumerable<string>? slots = null)
    {
        var result = _items.Where(i => _itemLayer.GetValueOrDefault(i.Id) == layer);
        if (slots is not null)
        {
            var slotSet = slots.ToHashSet();
            result = result.Where(i => i.Slots.Any(slotSet.Contains));
        }

        return result;
    }

    public int? LayerOf(ClothingItem item)
    {
        return _itemLayer.TryGetValue(item.Id, out var layer) ? layer : null;
    }

    public bool CanAddOnLayer(ClothingItem item, int layer)
    {
        if (!item.Layers.Contains(layer))
        {
            return false;
        }

        var used = _usedSlots.GetValueOrDefault(layer) ?? [];
        return !item.Slots.Any(used.Contains);
    }

    public bool AddItemOnLayer(ClothingItem item, int layer)
    {
        if (!CanAddOnLayer(item, layer))
        {
            return false;
        }

        if (_items.Contains(item))
        {
            var oldLayer = _itemLayer.GetValueOrDefault(item.Id, -1);
            if (oldLayer == layer)
            {
                return true;
            }

            if (oldLayer >= 0)
            {
                foreach (var s in item.Slots)
                {
                    _usedSlots.GetValueOrDefault(oldLayer)?.Remove(s);
                }
            }
        }
        else
        {
            _items.Add(item);
        }

        _itemLayer[item.Id] = layer;
        if (!_usedSlots.ContainsKey(layer))
        {
            _usedSlots[layer] = [];
        }

        foreach (var s in item.Slots)
        {
            _usedSlots[layer].Add(s);
        }

        return true;
    }

    public bool RemoveItem(ClothingItem item)
    {
        if (!_items.Contains(item))
        {
            return false;
        }

        if (_itemLayer.TryGetValue(item.Id, out var layer))
        {
            foreach (var s in item.Slots)
            {
                _usedSlots.GetValueOrDefault(layer)?.Remove(s);
            }

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
            if (layerItems.Count == 0)
            {
                continue;
            }

            lines.Add($"{layer}:");
            foreach (var it in layerItems)
            {
                lines.Add($"  • {it.Name} ({string.Join(", ", it.Slots.Order())})");
            }
        }

        lines.Add($"Total Multiplier: x{TotalMultiplier:F2}");
        return string.Join("\n", lines);
    }
}
