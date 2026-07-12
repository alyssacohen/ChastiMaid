namespace ChastiMaid.Models;

public static class MotivationalQuoteBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> QuoteKeys =
    [
        "MotivationalQuoteDuty",
        "MotivationalQuotePerseverance",
        "MotivationalQuoteDiscipline",
        "MotivationalQuoteGrace"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return QuoteKeys[(rng ?? Rng).Next(QuoteKeys.Count)];
    }
}
