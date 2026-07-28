using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

public record DiscardedOutfitRuleInfo(
    int RuleId,
    int ItemId,
    int RelatedItemId,
    OutfitRelation Relation,
    List<int> MissingItemIds);

public record ImportResult(
    int WardrobeImported,
    int RulesImported,
    int RulesDiscarded,
    List<DiscardedOutfitRuleInfo> DiscardedRules,
    int TasksImported);
