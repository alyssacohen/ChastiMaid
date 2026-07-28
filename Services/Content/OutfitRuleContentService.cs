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
    ///     Discards any rule referencing a wardrobe item id that doesn't exist. <see cref="OutfitRelation.Requires" />
    ///     is valid if at least one of ItemId/RelatedItemId exists (mirrors the OR semantics <c>OutfitBuilder</c>
    ///     applies across multiple Requires rows sharing an ItemId); <see cref="OutfitRelation.Incompatible" />
    ///     requires both ids to exist, since <c>OutfitBuilder</c> treats it as a symmetric pair at runtime.
    /// </summary>
    public (List<OutfitRule> Valid, List<DiscardedOutfitRuleInfo> Discarded) Validate(
        List<OutfitRule> rules, IReadOnlyCollection<int> validItemIds)
    {
        var validIds = validItemIds.ToHashSet();
        var valid = new List<OutfitRule>();
        var discarded = new List<DiscardedOutfitRuleInfo>();

        foreach (var rule in rules)
        {
            var itemOk = validIds.Contains(rule.ItemId);
            var relatedOk = validIds.Contains(rule.RelatedItemId);
            var ok = rule.Relation == OutfitRelation.Requires ? itemOk || relatedOk : itemOk && relatedOk;

            if (ok)
            {
                valid.Add(rule);
                continue;
            }

            var missing = new List<int>();
            if (!itemOk)
            {
                missing.Add(rule.ItemId);
            }

            if (!relatedOk)
            {
                missing.Add(rule.RelatedItemId);
            }

            discarded.Add(new DiscardedOutfitRuleInfo(rule.Id, rule.ItemId, rule.RelatedItemId, rule.Relation, missing));
        }

        return (valid, discarded);
    }
}
