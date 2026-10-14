using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class SummonPhraseBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> PhraseKeys =
    [
        Localizer.Mark("{0}! Come here at once."),
        Localizer.Mark("Get over here, {0}! You know better than keep me waiting."),
        Localizer.Mark("{0}! Do stop dawdling and come here."),
        Localizer.Mark("{0}! Here. Now.")
    ];

    public static string GetRandom(Random? rng = null)
    {
        return PhraseKeys[(rng ?? Rng).Next(PhraseKeys.Count)];
    }
}
