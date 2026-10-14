using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class HydrationReminderBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> ReminderKeys =
    [
        Localizer.Mark("The body, like the house, requires its proper upkeep. Go and drink half a cup."),
        Localizer.Mark("A moment's pause for water is no idleness. Go and take five sips of your drink."),
        Localizer.Mark("Even diligence requires fuel. Drink one cup."),
        Localizer.Mark("You may pause to drink. I shall not think less of you for it. Drink half a glass.")
    ];

    public static string GetRandom(Random? rng = null)
    {
        return ReminderKeys[(rng ?? Rng).Next(ReminderKeys.Count)];
    }
}
