namespace ChastiMaid.Models;

public static class HydrationReminderBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> ReminderKeys =
    [
        "HydrationReminderHousehold",
        "HydrationReminderRoutine",
        "HydrationReminderDuty",
        "HydrationReminderPause"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return ReminderKeys[(rng ?? Rng).Next(ReminderKeys.Count)];
    }
}
