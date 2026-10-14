using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class SelfCheckInstructionBank
{
    private static readonly Random Rng = new();

    private static readonly List<string> InstructionKeys =
    [
        Localizer.Mark("Cast your mind back. Does {0} appear in this photograph?"),
        Localizer.Mark("I shall test your memory. Is {0} visible here?"),
        Localizer.Mark("A small audit, nothing more. Tell me truthfully — is {0} to be seen in this image?")
    ];

    public static string GetRandom(Random? rng = null)
    {
        return InstructionKeys[(rng ?? Rng).Next(InstructionKeys.Count)];
    }
}
