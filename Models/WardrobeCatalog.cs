using System.Globalization;
using System.Text.Json.Serialization;
using LiteDB;

namespace ChastiMaid.Models;

/// <summary>
///     The user-managed lists of wardrobe slots and tags, kept even while no item uses an entry. Singleton
///     document (Id 1) in the profile. The items stay the source of truth: <see cref="Merge" /> always adds every
///     value the items (and, for tags, the rules) use, so a profile or contract without a catalogue gets one
///     derived from its data.
/// </summary>
public class WardrobeCatalog
{
    [JsonIgnore] [BsonId] public int Id { get; set; } = 1; // singleton
    public List<string> Slots { get; set; } = [];
    public List<string> Tags { get; set; } = [];

    /// <summary>Display and storage order of slots and tags: alphabetical in the UI language, ignoring case.</summary>
    public static StringComparer Order => StringComparer.Create(CultureInfo.CurrentUICulture, true);

    /// <summary>The stored lists plus every slot and tag in use, normalized (see <see cref="Normalize" />).</summary>
    public static WardrobeCatalog Merge(WardrobeCatalog? stored, IEnumerable<ClothingItem> items,
        IEnumerable<OutfitRule> rules)
    {
        var itemList = items.ToList();
        return new WardrobeCatalog
        {
            Slots = Normalize((stored?.Slots ?? []).Concat(itemList.SelectMany(i => i.Slots))),
            Tags = Normalize((stored?.Tags ?? []).Concat(itemList.SelectMany(i => i.Tags))
                .Concat(rules.SelectMany(r => new[] { r.SourceTag, r.RelatedTag }))),
        };
    }

    /// <summary>Trimmed, non-empty values, deduplicated ignoring case (the first spelling wins), in <see cref="Order" />.</summary>
    public static List<string> Normalize(IEnumerable<string?> values) =>
    [
        .. values.Select(v => v?.Trim()).OfType<string>().Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(Order),
    ];
}
