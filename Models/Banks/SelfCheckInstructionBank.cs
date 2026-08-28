namespace ChastiMaid.Models;

public static class SelfCheckInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        "SelfCheckInstructionRecall",
        "SelfCheckInstructionInspect",
        "SelfCheckInstructionAudit"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
