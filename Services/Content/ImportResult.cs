using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

public record DiscardedOutfitRuleInfo(
    int RuleId,
    string SourceTag,
    string RelatedTag,
    OutfitRelation Relation,
    List<string> MissingTags);

public record ImportResult(
    int WardrobeImported,
    int RulesImported,
    int RulesDiscarded,
    List<DiscardedOutfitRuleInfo> DiscardedRules,
    int TasksImported);
