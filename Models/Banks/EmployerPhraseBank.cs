using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class EmployerPhraseBank
{
    // Untranslated source texts: they are stored in saves (Writing Lines) and translated when shown.
    private static readonly List<string> Phrases =
    [
        Localizer.Mark("I shall attend to my duties without requiring reminder"),
        Localizer.Mark("Punctuality is a courtesy I extend to no one more than myself"),
        Localizer.Mark("I am fortunate indeed to be so thoroughly looked after"),
        Localizer.Mark("I keep a well-ordered house by keeping a well-ordered self"),
        Localizer.Mark("My untidiness is merely a decision I have yet to unmake"),
        Localizer.Mark("I shall speak when addressed, and act before then"),
        Localizer.Mark("I know a task delayed is a task doubled, and I shall not double it"),
        Localizer.Mark("I have not asked twice, and I do not intend to"),
        Localizer.Mark("Effort I spare today is effort I shall owe tomorrow, with interest"),
        Localizer.Mark("I shall wait as long as is required, and not a moment more visibly"),
        Localizer.Mark("Respect, once earned, is a debt I shall not fail to repay"),
        Localizer.Mark("A task half done is a task I have not yet begun"),
        Localizer.Mark("My duty is not diminished by repetition. It is proven by it"),
        Localizer.Mark("A task I begin in earnest is a task I count already half completed"),
        Localizer.Mark("My discipline is merely devotion with its sleeves rolled up"),
        Localizer.Mark("I show grace under a modest burden, and call it the truest mark of breeding")
    ];

    public static IReadOnlyList<string> All => Phrases;

    public static string GetRandom(Random? rng = null)
    {
        return Phrases[(rng ?? Random.Shared).Next(Phrases.Count)];
    }

    /// <summary>Picks <paramref name="count" /> distinct phrases in random order.</summary>
    public static List<string> GetRandomSet(int count, Random? rng = null)
    {
        rng ??= Random.Shared;
        return Phrases.OrderBy(_ => rng.Next()).Take(count).ToList();
    }
}
