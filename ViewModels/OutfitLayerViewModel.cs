namespace ChastiMaid.ViewModels;

public class OutfitLayerViewModel : ViewModelBase
{
    public string LayerLabel { get; }
    public string ItemName { get; }
    public string ItemDetail { get; }
    public bool HasItem => ItemName != "(none)";

    public OutfitLayerViewModel(string layerLabel, string itemName = "(none)", string itemDetail = "")
    {
        LayerLabel = layerLabel;
        ItemName = itemName;
        ItemDetail = itemDetail;
    }
}
