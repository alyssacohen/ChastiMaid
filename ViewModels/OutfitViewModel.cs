using ChastiMaid.Models;

namespace ChastiMaid.ViewModels;

public class OutfitViewModel : ViewModelBase
{
    public OutfitViewModel(IReadOnlyList<OutfitLayerViewModel> layers)
    {
        Layers = layers;
    }

    public IReadOnlyList<OutfitLayerViewModel> Layers { get; }

    /// <summary>
    ///     Parses the text produced by <see cref="Models.Outfit.ToString" /> into an
    ///     <see cref="OutfitViewModel" /> with one row per dressing layer.
    ///     Section headers are int layer orders ("0:", "3:", etc.).
    /// </summary>
    public static OutfitViewModel FromString(string? outfitText)
    {
        var layerItems = new Dictionary<int, List<(string Name, IReadOnlyList<string> Zones)>>();

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

                    layerItems[currentLayer.Value].Add((name, zones));
                }
            }
        }

        var layers = WardrobeLayer.All.Select(def =>
        {
            if (layerItems.TryGetValue(def.Order, out var items) && items.Count > 0)
            {
                return new OutfitLayerViewModel(def.DisplayName, def.Order, items[0].Name, items[0].Zones);
            }

            return new OutfitLayerViewModel(def.DisplayName, def.Order);
        }).ToList();

        return new OutfitViewModel(layers);
    }
}
