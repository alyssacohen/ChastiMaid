namespace ChastiMaid.Models;

public static class KneelInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        "KneelInstructionReflect",
        "KneelInstructionHumility",
        "KneelInstructionStillness"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
