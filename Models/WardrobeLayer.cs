using ChastiMaid.Localization;

namespace ChastiMaid.Models;

/// <summary>
///     Definition of a single dressing layer, ordered from innermost (Order 0) to outermost.
///     Order is the stable int identifier used in persistence; DisplayName/Description are untranslated source
///     texts for display, to be passed through <see cref="ILocalizer.T(string)" />.
/// </summary>
public record WardrobeLayer(
    int Order,
    string DisplayName,
    string Description)
{
    public static readonly IReadOnlyList<WardrobeLayer> All =
    [
        // translators: wardrobe layer names, each with a short hint on what belongs to it
        new(0, Localizer.Mark("Underwear"), Localizer.Mark("foundation · choose freely")),
        new(1, Localizer.Mark("Extra"), Localizer.Mark("suspenders, garters · optional unless required by hosiery")),
        new(2, Localizer.Mark("Base"), Localizer.Mark("hosiery, slip · choose at most one pair")),
        new(3, Localizer.Mark("Outerwear"), Localizer.Mark("dress, skirt, blouse")),
        new(4, Localizer.Mark("Accessories"), Localizer.Mark("shoes, jewellery · multiple allowed")),
    ];

    public static WardrobeLayer? FindByOrder(int order)
    {
        return All.FirstOrDefault(l => l.Order == order);
    }
}
