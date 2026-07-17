namespace ChastiMaid.Models;

/// <summary>Severity ranges rolled when an infraction is punished. Not exposed in Settings.</summary>
public static class InfractionSeverity
{
    public static readonly (int Min, int Max) PopupMissed = (3, 5);
    public static readonly (int Min, int Max) ImpossibleQuestionFailed = (6, 9);
    public static readonly (int Min, int Max) ChoreTooEarly = (7, 10);
}
