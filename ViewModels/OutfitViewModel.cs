namespace ChastiMaid.ViewModels;

public class OutfitViewModel : ViewModelBase
{
    private static readonly (string Label, string Key)[] LayerDefs =
    [
        ("UNDERWEAR", "undies"),
        ("EXTRA",     "extra"),
        ("BASE",      "base"),
        ("OUTERWEAR", "outer"),
        ("ACCESSORIES", "gears"),
    ];

    public IReadOnlyList<OutfitLayerViewModel> Layers { get; }

    public OutfitViewModel(IReadOnlyList<OutfitLayerViewModel> layers)
    {
        Layers = layers;
    }

    /// <summary>
    /// Parses the text produced by <see cref="Models.Outfit.ToString"/> into an
    /// <see cref="OutfitViewModel"/> with one row per dressing layer.
    /// </summary>
    public static OutfitViewModel FromString(string? outfitText)
    {
        var layerItems = new Dictionary<string, List<(string Name, string Detail)>>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(outfitText))
        {
            string? currentLayer = null;
            foreach (var raw in outfitText.Split('\n'))
            {
                var line = raw.Trim();
                if (line.EndsWith(':') && !line.StartsWith('•'))
                {
                    currentLayer = line[..^1];
                }
                else if (line.StartsWith('•') && currentLayer is not null)
                {
                    // "• Item Name (slot1, slot2)"
                    var content = line[1..].Trim();
                    var parenIdx = content.LastIndexOf('(');
                    string name, detail;
                    if (parenIdx > 0 && content.EndsWith(')'))
                    {
                        name   = content[..parenIdx].Trim();
                        detail = content[(parenIdx + 1)..^1];
                    }
                    else
                    {
                        name   = content;
                        detail = "";
                    }
                    if (!layerItems.ContainsKey(currentLayer))
                        layerItems[currentLayer] = [];
                    layerItems[currentLayer].Add((name, detail));
                }
            }
        }

        var layers = LayerDefs.Select(def =>
        {
            if (layerItems.TryGetValue(def.Key, out var items) && items.Count > 0)
                return new OutfitLayerViewModel(def.Label, items[0].Name, items[0].Detail);
            return new OutfitLayerViewModel(def.Label);
        }).ToList();

        return new OutfitViewModel(layers);
    }
}
