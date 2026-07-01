namespace ChastiMaid.Models;

/// <summary>
///     Definition of a single dressing layer, ordered from innermost (Order 0) to outermost.
///     Order is the stable int identifier used in persistence; DisplayName/Description are the display surface.
/// </summary>
public record WardrobeLayer(
    int Order,
    string DisplayName,
    string Description)
{
    public static readonly IReadOnlyList<WardrobeLayer> All =
    [
        new(0, "Underwear", "foundation · choose freely"),
        new(1, "Extra", "suspenders, garters · optional unless required by hosiery"),
        new(2, "Base", "hosiery, slip · choose at most one pair"),
        new(3, "Outerwear", "dress, skirt, blouse"),
        new(4, "Accessories", "shoes, jewellery · multiple allowed")
    ];

    public static WardrobeLayer? FindByOrder(int order)
    {
        return All.FirstOrDefault(l => l.Order == order);
    }
}
