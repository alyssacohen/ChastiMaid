namespace ChastiMaid.Services.Content;

public record DiscardedOutfitRuleInfo(List<string> MissingTags);

public record ImportResult(
    int WardrobeImported,
    int RulesImported,
    int RulesDiscarded,
    List<DiscardedOutfitRuleInfo> DiscardedRules,
    int TasksImported);
