namespace ChastiMaid.Models;

public static class WritingLinesSentenceBank
{
    private static readonly List<string> SentenceKeys =
    [
        "WritingLineObedience",
        "WritingLinePunctuality",
        "WritingLineGratitude",
        "WritingLineDiscipline"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return SentenceKeys[(rng ?? Random.Shared).Next(SentenceKeys.Count)];
    }
}
