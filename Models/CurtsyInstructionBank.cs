namespace ChastiMaid.Models;

public static class CurtsyInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        "CurtsyInstructionFormal",
        "CurtsyInstructionGrace",
        "CurtsyInstructionRespect"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
