using ChastiMaid.Localization;
using ChastiMaid.Models;

namespace ChastiMaid.ViewModels;

public class OutfitViewModel : ViewModelBase
{
    public OutfitViewModel(IReadOnlyList<OutfitLayerViewModel> layers)
    {
        Layers = layers;
    }

    public IReadOnlyList<OutfitLayerViewModel> Layers { get; }

    /// <summary>"5 items · 3 layers covered".</summary>
    public string CountSummary(ILocalizer l)
    {
        var items = Layers.Sum(x => x.Items.Count);
        var layers = Layers.Count(x => x.HasItems);
        return l.N("{0} item", "{0} items", items, items) + " · " +
               l.N("{0} layer covered", "{0} layers covered", layers, layers);
    }

    /// <summary>
    ///     Parses the text produced by <see cref="Models.Outfit.ToString" /> into an
    ///     <see cref="OutfitViewModel" /> with one row per dressing layer.
    ///     Section headers are int layer orders ("0:", "3:", etc.).
    /// </summary>
    public static OutfitViewModel FromString(string? outfitText, ILocalizer l, ClothingItem? plugAndPlayItem = null)
    {
        var layerItems = new Dictionary<int, List<(string Name, IReadOnlyList<string> Zones, bool IsPlugAndPlay)>>();

        if (!string.IsNullOrWhiteSpace(outfitText))
        {
            int? currentLayer = null;
            foreach (var raw in outfitText.Split('\n'))
            {
                var line = raw.Trim();
                if (line.EndsWith(':') && !line.StartsWith('•'))
                {
                    var header = line[..^1];
                    currentLayer = int.TryParse(header, out var order) ? order : null;
                }
                else if (line.StartsWith('•') && currentLayer is not null)
                {
                    // "• Item Name (slot1, slot2)"
                    var content = line[1..].Trim();
                    var parenIdx = content.LastIndexOf('(');
                    string name;
                    List<string> zones;
                    if (parenIdx > 0 && content.EndsWith(')'))
                    {
                        name = content[..parenIdx].Trim();
                        zones = content[(parenIdx + 1)..^1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    }
                    else
                    {
                        name = content;
                        zones = [];
                    }

                    if (!layerItems.ContainsKey(currentLayer.Value))
                    {
                        layerItems[currentLayer.Value] = [];
                    }

                    layerItems[currentLayer.Value].Add((name, zones, false));
                }
            }
        }

        if (plugAndPlayItem is not null)
        {
            var layer = plugAndPlayItem.Layers.FirstOrDefault();
            if (!layerItems.TryGetValue(layer, out var pnpList))
            {
                pnpList = [];
                layerItems[layer] = pnpList;
            }

            pnpList.Add((plugAndPlayItem.Name, plugAndPlayItem.Slots, true));
        }

        var layers = WardrobeLayer.All.Select(def =>
        {
            var items = layerItems.TryGetValue(def.Order, out var list)
                ? list.Select(i => new OutfitLayerItem(i.Name, i.Zones, i.IsPlugAndPlay)).ToList()
                : [];
            return new OutfitLayerViewModel(l.T(def.DisplayName), def.Order, items);
        }).ToList();

        return new OutfitViewModel(layers);
    }
}
