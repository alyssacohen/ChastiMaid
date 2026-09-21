namespace ChastiMaid.Models;

public static class SummonPhraseBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> PhraseKeys =
    [
        "SummonAtOnceFormat",
        "SummonKeepWaitingFormat",
        "SummonDawdlingFormat",
        "SummonHereNowFormat"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return PhraseKeys[(rng ?? Rng).Next(PhraseKeys.Count)];
    }
}
