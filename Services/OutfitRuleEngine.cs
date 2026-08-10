using ChastiMaid.Models;

namespace ChastiMaid.Services;

/// <summary>
///     Resolves Requires/Incompatible <see cref="OutfitRule" /> rows against <see cref="ClothingItem" /> tags.
///     Rules match on tags rather than specific items, so a single rule row (e.g. "reggicalze" requires "calze da
///     aggancio") applies to every item carrying either tag. Shared by <see cref="OutfitBuilder" /> (auto-generation),
///     the outfit chip editors, and <see cref="ViewModels.DashboardViewModel" />'s plug-and-play eligibility check.
/// </summary>
public class OutfitRuleEngine(List<OutfitRule> rules)
{
    public static bool TagsEqual(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool HasTag(ClothingItem item, string tag) => item.Tags.Any(t => TagsEqual(t, tag));

    /// <summary>True if an Incompatible rule pairs a tag on <paramref name="a" /> with a tag on <paramref name="b" />, in either direction.</summary>
    public bool AreIncompatible(ClothingItem a, ClothingItem b)
    {
        foreach (var rule in rules.Where(r => r.Relation == OutfitRelation.Incompatible))
        {
            var aHasSource = HasTag(a, rule.SourceTag);
            var aHasRelated = HasTag(a, rule.RelatedTag);
            var bHasSource = HasTag(b, rule.SourceTag);
            var bHasRelated = HasTag(b, rule.RelatedTag);
            if ((aHasSource && bHasRelated) || (aHasRelated && bHasSource))
            {
                return true;
            }
        }

        return false;
    }

    public bool ConflictsWithAny(ClothingItem candidate, IEnumerable<ClothingItem> others) =>
        others.Any(other => AreIncompatible(candidate, other));

    /// <summary>Distinct related tags required by any Requires rule whose source tag matches one of <paramref name="item" />'s tags.</summary>
    public IReadOnlyList<string> RequiredTagsFor(ClothingItem item)
    {
        var result = new List<string>();
        foreach (var rule in rules.Where(r => r.Relation == OutfitRelation.Requires))
        {
            if (HasTag(item, rule.SourceTag) && result.All(t => !TagsEqual(t, rule.RelatedTag)))
            {
                result.Add(rule.RelatedTag);
            }
        }

        return result;
    }

    /// <summary>True if none of <paramref name="item" />'s Requires rules apply, or at least one required tag is present among <paramref name="placedOrSelected" />.</summary>
    public bool RequiresSatisfied(ClothingItem item, IEnumerable<ClothingItem> placedOrSelected)
    {
        var required = RequiredTagsFor(item);
        if (required.Count == 0)
        {
            return true;
        }

        var pool = placedOrSelected.ToList();
        return required.Any(tag => pool.Any(other => HasTag(other, tag)));
    }

    public IEnumerable<ClothingItem> ItemsWithAnyTag(IEnumerable<ClothingItem> pool, IEnumerable<string> tags)
    {
        var tagList = tags.ToList();
        return pool.Where(item => tagList.Any(tag => HasTag(item, tag)));
    }
}
