namespace ChastiMaid.Models;

public static class WritingLinesSentenceBank
{
    private static readonly List<string> SentenceKeys =
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
        "WritingLineThoroughness"
    ];

    public static IReadOnlyList<string> AllKeys => SentenceKeys;

    public static string GetRandom(Random? rng = null)
    {
        return SentenceKeys[(rng ?? Random.Shared).Next(SentenceKeys.Count)];
    }

    /// <summary>Picks <paramref name="count" /> distinct keys in random order.</summary>
    public static List<string> GetRandomSet(int count, Random? rng = null)
    {
        rng ??= Random.Shared;
        return SentenceKeys.OrderBy(_ => rng.Next()).Take(count).ToList();
    }
}
