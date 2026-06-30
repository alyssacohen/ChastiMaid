namespace ChastiMaid.Models;

/// <summary>
///     Definition of a single dressing layer, ordered from innermost (Order 0) to outermost.
///     Order is the stable int identifier used in persistence; DisplayName/Description are the display surface.
/// </summary>
public record WardrobeLayer(
    int Order,
    string DisplayName,
    string Description,
    double Probability)
{
    public static readonly IReadOnlyList<WardrobeLayer> All =
    [
        new(0, "Underwear", "foundation · choose freely", 1.0),
        new(1, "Extra", "suspenders, garters · optional unless required by hosiery", 0.7),
        new(2, "Base", "hosiery, slip · choose at most one pair", 1.0),
        new(3, "Outerwear", "dress, skirt, blouse", 1.0),
        new(4, "Accessories", "shoes, jewellery · multiple allowed", 0.5)
    ];

    public static WardrobeLayer? FindByOrder(int order)
    {
        return All.FirstOrDefault(l => l.Order == order);
    }
}
