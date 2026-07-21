namespace ChastiMaid.Models;

public static class EmployerPhraseBank
{
    private static readonly List<string> PhraseKeys =
    [
        "WritingLineObedience",
        "WritingLinePunctuality",
        "WritingLineGratitude",
        "WritingLineDiscipline",
        "WritingLineTidiness",
        "WritingLineDeference",
        "WritingLinePromptness",
        "WritingLineHumility",
        "WritingLineDiligence",
        "WritingLinePatience",
        "WritingLineRespect",
        "WritingLineThoroughness",
        "MotivationalQuoteDuty",
        "MotivationalQuotePerseverance",
        "MotivationalQuoteDiscipline",
        "MotivationalQuoteGrace"
    ];

    public static IReadOnlyList<string> AllKeys => PhraseKeys;

    public static string GetRandom(Random? rng = null)
    {
        return PhraseKeys[(rng ?? Random.Shared).Next(PhraseKeys.Count)];
    }

    /// <summary>Picks <paramref name="count" /> distinct keys in random order.</summary>
    public static List<string> GetRandomSet(int count, Random? rng = null)
    {
        rng ??= Random.Shared;
        return PhraseKeys.OrderBy(_ => rng.Next()).Take(count).ToList();
    }
}
