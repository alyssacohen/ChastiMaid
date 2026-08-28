namespace ChastiMaid.Models;

public static class PermissionPhraseBank
{
    private static readonly List<string> GrantedKeys =
    [
        "PermissionGrantedGracious",
        "PermissionGrantedBrief",
        "PermissionGrantedIndulgent",
        "PermissionGrantedReluctant",
    ];

    private static readonly List<string> DeniedKeys =
    [
        "PermissionDeniedBusy",
        "PermissionDeniedNotNow",
        "PermissionDeniedWhim",
        "PermissionDeniedDiscipline",
    ];

    private static readonly List<string> DeniedInsistenceKeys =
    [
        "PermissionDeniedInsistenceAnnoyed",
        "PermissionDeniedInsistenceWarning",
        "PermissionDeniedInsistenceMocking",
    ];

    private static readonly List<string> DeniedDiaperKeys =
    [
        "PermissionDeniedDiaperReminder",
        "PermissionDeniedDiaperMocking",
        "PermissionDeniedDiaperObvious",
    ];

    public static string GetRandomGranted(Random? rng = null)
    {
        return GrantedKeys[(rng ?? Random.Shared).Next(GrantedKeys.Count)];
    }

    public static string GetRandomDenied(Random? rng = null)
    {
        return DeniedKeys[(rng ?? Random.Shared).Next(DeniedKeys.Count)];
    }

    public static string GetRandomDeniedInsistence(Random? rng = null)
    {
        return DeniedInsistenceKeys[(rng ?? Random.Shared).Next(DeniedInsistenceKeys.Count)];
    }

    public static string GetRandomDeniedDiaper(Random? rng = null)
    {
        return DeniedDiaperKeys[(rng ?? Random.Shared).Next(DeniedDiaperKeys.Count)];
    }
}
