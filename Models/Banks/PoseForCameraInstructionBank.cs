namespace ChastiMaid.Models;

public static class PoseForCameraInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        "PoseForCameraInstructionFormal",
        "PoseForCameraInstructionInspect",
        "PoseForCameraInstructionProof"
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
