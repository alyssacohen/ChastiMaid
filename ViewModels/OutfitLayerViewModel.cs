namespace ChastiMaid.ViewModels;

public class OutfitLayerViewModel : ViewModelBase
{
    public OutfitLayerViewModel(string layerLabel, int layerOrder, string itemName = "(none)", IReadOnlyList<string>? zones = null)
    {
        LayerLabel = layerLabel;
        LayerOrder = layerOrder;
        ItemName = itemName;
        Zones = zones ?? [];
    }

    public string LayerLabel { get; }
    public int LayerOrder { get; }
    public string ItemName { get; }
    public IReadOnlyList<string> Zones { get; }
    public bool HasItem => ItemName != "(none)";
    public string LayerIndex => $"Layer {LayerOrder}";
    public string ItemDetail => string.Join(", ", Zones);
    public string ZonesDisplay => string.Join(" · ", Zones);
}
