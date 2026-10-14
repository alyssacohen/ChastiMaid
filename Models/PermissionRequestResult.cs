namespace ChastiMaid.Models;

public enum PermissionRequestOutcome
{
    Granted,
    Denied,
    DeniedInsistence,
    DeniedDiaper,
}

public record PermissionRequestResult(PermissionRequestOutcome Outcome, string Phrase);
