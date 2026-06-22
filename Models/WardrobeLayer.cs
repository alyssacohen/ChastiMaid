namespace ChastiMaid.Models;

/// <summary>
/// Definition of a single dressing layer, ordered from innermost (Order 0) to outermost.
/// Key is stable and used for persistence; DisplayName/Label/Description are the display surface.
/// </summary>
public record WardrobeLayer(
    string Key,
    int    Order,
    string DisplayName,
    string Description,
    string Label,
    double Probability)
{
    public static readonly IReadOnlyList<WardrobeLayer> All =
    [
        new("undies", 0, "Underwear",    "foundation · choose freely",                                  "UNDERWEAR",   1.0),
        new("extra",  1, "Extra",        "suspenders, garters · optional unless required by hosiery",   "EXTRA",       0.7),
        new("base",   2, "Base",         "hosiery, slip · choose at most one pair",                     "BASE",        1.0),
        new("outer",  3, "Outerwear",    "dress, skirt, blouse",                                        "OUTERWEAR",   1.0),
        new("gears",  4, "Accessories",  "shoes, jewellery · multiple allowed",                         "ACCESSORIES", 0.5),
    ];

    public static WardrobeLayer? FindByKey(string key) =>
        All.FirstOrDefault(l => l.Key == key);
}
