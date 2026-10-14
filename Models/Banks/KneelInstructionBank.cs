using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class KneelInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        Localizer.Mark("Now kneel, look down, and reflect upon your failings."),
        Localizer.Mark("Kneel where you stand, and remain a while with your humility."),
        Localizer.Mark("Down upon your knees. Stillness becomes you.")
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
