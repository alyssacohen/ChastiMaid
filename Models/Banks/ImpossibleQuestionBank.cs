namespace ChastiMaid.Models;

public record ImpossibleQuestion(
    string QuestionKey,
    string YesPositiveKey,
    string YesNegativeKey,
    string NoPositiveKey,
    string NoNegativeKey);

public static class ImpossibleQuestionBank
{
    private static readonly Random Rng = new();

    private static readonly List<ImpossibleQuestion> Questions =
    [
        new("ImpossibleQuestionDiligence",
            "ImpossibleQuestionDiligenceYesPositive", "ImpossibleQuestionDiligenceYesNegative",
            "ImpossibleQuestionDiligenceNoPositive", "ImpossibleQuestionDiligenceNoNegative"),
        new("ImpossibleQuestionPerfection",
            "ImpossibleQuestionPerfectionYesPositive", "ImpossibleQuestionPerfectionYesNegative",
            "ImpossibleQuestionPerfectionNoPositive", "ImpossibleQuestionPerfectionNoNegative"),
        new("ImpossibleQuestionContentment",
            "ImpossibleQuestionContentmentYesPositive", "ImpossibleQuestionContentmentYesNegative",
            "ImpossibleQuestionContentmentNoPositive", "ImpossibleQuestionContentmentNoNegative"),
        new("ImpossibleQuestionInitiative",
            "ImpossibleQuestionInitiativeYesPositive", "ImpossibleQuestionInitiativeYesNegative",
            "ImpossibleQuestionInitiativeNoPositive", "ImpossibleQuestionInitiativeNoNegative"),
        new("ImpossibleQuestionFatigue",
            "ImpossibleQuestionFatigueYesPositive", "ImpossibleQuestionFatigueYesNegative",
            "ImpossibleQuestionFatigueNoPositive", "ImpossibleQuestionFatigueNoNegative"),
        new("ImpossibleQuestionComplaints",
            "ImpossibleQuestionComplaintsYesPositive", "ImpossibleQuestionComplaintsYesNegative",
            "ImpossibleQuestionComplaintsNoPositive", "ImpossibleQuestionComplaintsNoNegative"),
        new("ImpossibleQuestionImprovement",
            "ImpossibleQuestionImprovementYesPositive", "ImpossibleQuestionImprovementYesNegative",
            "ImpossibleQuestionImprovementNoPositive", "ImpossibleQuestionImprovementNoNegative"),
        new("ImpossibleQuestionVirtue",
            "ImpossibleQuestionVirtueYesPositive", "ImpossibleQuestionVirtueYesNegative",
            "ImpossibleQuestionVirtueNoPositive", "ImpossibleQuestionVirtueNoNegative")
    ];

    public static ImpossibleQuestion GetRandom()
    {
        return Questions[Rng.Next(Questions.Count)];
    }

    /// <summary>The outcome is deliberately independent of which answer the player gave.</summary>
    public static bool RollOutcome(Random? rng = null)
    {
        return (rng ?? Rng).Next(2) == 0;
    }
}
