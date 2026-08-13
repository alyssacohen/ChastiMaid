using System.Text.Json;
using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

public class OutfitRuleContentService
{
    public List<OutfitRule> Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<OutfitRule>>(json, ContentJsonOptions.Options) ?? [];
    }

    public void Export(List<OutfitRule> rules, string filePath)
    {
        File.WriteAllText(filePath, JsonSerializer.Serialize(rules, ContentJsonOptions.Options));
    }

    /// <summary>
    ///     Discards any rule referencing a wardrobe tag that doesn't exist. <see cref="OutfitRelation.Requires" />
    ///     is valid if at least one of SourceTag/RelatedTag exists (mirrors the OR semantics <c>OutfitBuilder</c>
    ///     applies across multiple Requires rows sharing a SourceTag); <see cref="OutfitRelation.Incompatible" />
    ///     requires both tags to exist, since <c>OutfitBuilder</c> treats it as a symmetric pair at runtime.
    /// </summary>
    public (List<OutfitRule> Valid, List<DiscardedOutfitRuleInfo> Discarded) Validate(
        List<OutfitRule> rules, IReadOnlyCollection<string> validTags)
    {
        var validTagSet = validTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var valid = new List<OutfitRule>();
        var discarded = new List<DiscardedOutfitRuleInfo>();

        foreach (var rule in rules)
        {
            var sourceOk = validTagSet.Contains(rule.SourceTag);
            var relatedOk = validTagSet.Contains(rule.RelatedTag);
            var ok = rule.Relation == OutfitRelation.Requires ? sourceOk || relatedOk : sourceOk && relatedOk;

            if (ok)
            {
                valid.Add(rule);
                continue;
            }

            var missing = new List<string>();
            if (!sourceOk)
            {
                missing.Add(rule.SourceTag);
            }

            if (!relatedOk)
            {
                missing.Add(rule.RelatedTag);
            }

            discarded.Add(new DiscardedOutfitRuleInfo(missing));
        }

        return (valid, discarded);
    }
}
