namespace ChastiMaid.ViewModels;

public class OutfitLayerViewModel : ViewModelBase
{
    public OutfitLayerViewModel(string layerLabel, int layerOrder, string itemName = "(none)", string itemDetail = "")
    {
        LayerLabel = layerLabel;
        LayerOrder = layerOrder;
        ItemName = itemName;
        ItemDetail = itemDetail;
    }

    public string LayerLabel { get; }
    public int LayerOrder { get; }
    public string ItemName { get; }
    public string ItemDetail { get; }
    public bool HasItem => ItemName != "(none)";
    public string LayerIndex => $"Layer {LayerOrder}";
    public string ZonesDisplay => string.Join(" · ", ItemDetail.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
}
