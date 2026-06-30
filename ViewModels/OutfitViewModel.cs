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
        var layerItems = new Dictionary<int, List<(string Name, string Detail)>>();

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
                    string name, detail;
                    if (parenIdx > 0 && content.EndsWith(')'))
                    {
                        name = content[..parenIdx].Trim();
                        detail = content[(parenIdx + 1)..^1];
                    }
                    else
                    {
                        name = content;
                        detail = "";
                    }

                    if (!layerItems.ContainsKey(currentLayer.Value))
                    {
                        layerItems[currentLayer.Value] = [];
                    }

                    layerItems[currentLayer.Value].Add((name, detail));
                }
            }
        }

        var layers = WardrobeLayer.All.Select(def =>
        {
            if (layerItems.TryGetValue(def.Order, out var items) && items.Count > 0)
            {
                return new OutfitLayerViewModel(def.Label, def.Order, items[0].Name, items[0].Detail);
            }

            return new OutfitLayerViewModel(def.Label, def.Order);
        }).ToList();

        return new OutfitViewModel(layers);
    }
}
