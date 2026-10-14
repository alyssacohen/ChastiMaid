using ChastiMaid.Localization;

namespace ChastiMaid.Models;

/// <summary>Employer replies to a permission request, as untranslated source texts (translate with ILocalizer.T).</summary>
public static class PermissionPhraseBank
{
    private static readonly List<string> Granted =
    [
        Localizer.Mark("Very well. See that it does not become a habit."),
        Localizer.Mark("Granted — briefly."),
        Localizer.Mark("I am feeling generous today. Do not test it."),
        Localizer.Mark("If you must. The clock is already ticking."),
    ];

    private static readonly List<string> Denied =
    [
        Localizer.Mark("Not at present. The work does not pause for you — nor do I."),
        Localizer.Mark("Denied. Ask again later, if you dare."),
        Localizer.Mark("No. I simply do not feel like it."),
        Localizer.Mark("Denied. Discipline does not negotiate."),
    ];

    private static readonly List<string> DeniedInsistence =
    [
        Localizer.Mark("I already answered that. Do not ask again so soon."),
        Localizer.Mark("Persistence will not change my mind — only my patience."),
        Localizer.Mark("Still no. Did you expect a different answer?"),
    ];

    private static readonly List<string> DeniedDiaper =
    [
        Localizer.Mark("You are wearing exactly what you need for that. Figure it out."),
        Localizer.Mark("How curious a request, given what you have on underneath."),
        Localizer.Mark("That question answers itself, does it not?"),
    ];

    public static string GetRandomGranted(Random? rng = null)
    {
        return Granted[(rng ?? Random.Shared).Next(Granted.Count)];
    }

    public static string GetRandomDenied(Random? rng = null)
    {
        return Denied[(rng ?? Random.Shared).Next(Denied.Count)];
    }

    public static string GetRandomDeniedInsistence(Random? rng = null)
    {
        return DeniedInsistence[(rng ?? Random.Shared).Next(DeniedInsistence.Count)];
    }

    public static string GetRandomDeniedDiaper(Random? rng = null)
    {
        return DeniedDiaper[(rng ?? Random.Shared).Next(DeniedDiaper.Count)];
    }
}
