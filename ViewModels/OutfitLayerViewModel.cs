namespace ChastiMaid.ViewModels;

public record OutfitLayerItem(string Name, IReadOnlyList<string> Zones, bool IsPlugAndPlay = false)
{
    public string ZonesDisplay => string.Join(" · ", Zones);
}

public class OutfitLayerViewModel : ViewModelBase
{
    public OutfitLayerViewModel(string layerLabel, int layerOrder, IReadOnlyList<OutfitLayerItem>? items = null)
    {
        LayerLabel = layerLabel;
        LayerOrder = layerOrder;
        Items = items ?? [];
    }

    public string LayerLabel { get; }
    public int LayerOrder { get; }
    public IReadOnlyList<OutfitLayerItem> Items { get; }
    public bool HasItems => Items.Count > 0;
    public string LayerIndex => $"Layer {LayerOrder}";
}
