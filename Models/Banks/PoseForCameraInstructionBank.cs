using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class PoseForCameraInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        Localizer.Mark("Present yourself before the camera and let {0} be plainly visible, that I may confirm it."),
        Localizer.Mark("I require a photograph in which {0} may be clearly inspected. See to it."),
        Localizer.Mark("You will pose for the camera. {0} must appear in the frame as proof.")
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
