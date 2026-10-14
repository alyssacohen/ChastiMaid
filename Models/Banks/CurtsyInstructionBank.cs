using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class CurtsyInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        Localizer.Mark("You will perform a curtsy before the camera, that I may witness your deportment."),
        Localizer.Mark("Rise, and offer me a proper curtsy. See that it is done with grace."),
        Localizer.Mark("A show of respect is due. Curtsy before me, and let it be recorded.")
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
