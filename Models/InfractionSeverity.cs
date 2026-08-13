namespace ChastiMaid.Models;

/// <summary>Severity ranges/values rolled when an infraction is punished. Not exposed in Settings.</summary>
public static class InfractionSeverity
{
    public static readonly (int Min, int Max) ImpossibleQuestionFailed = (3, 5); // combined with popup lateness

    public static readonly double PopupLateSeverityPerSecond = 0.5; // severity points per second past PopupTimeoutSeconds
    public static readonly int MotivationalQuoteFailedBonus = 5; // flat severity added on top of lateness when all 3 reading attempts fail
}
