using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public static class CornertimeInstructionBank
{
    private static readonly Random Rng = new();

    public static readonly List<string> PositionKeys =
    [
        Localizer.Mark("To the corner, if you please — feet together, hands clasped behind your back, and your face to the wall. You have ten seconds."),
        Localizer.Mark("The corner awaits. Feet together, hands behind your back, facing the wall. Do not dawdle."),
        Localizer.Mark("Into the corner. Feet together, hands clasped before you, eyes on the wall."),
        Localizer.Mark("To your corner — feet together, hands folded in front of you, and face the wall like a good girl should."),
        Localizer.Mark("To the corner — legs apart, hands behind your head, facing the wall. Ten seconds, starting now."),
        Localizer.Mark("The corner, please. Legs apart, hands behind your neck, and your face to the wall."),
        Localizer.Mark("Into the corner, feet together, hands upon your knees, facing the wall."),
        Localizer.Mark("To the corner. Feet together, hands resting on your knees, and face the wall — you know the drill by now.")
    ];

    public static readonly List<string> BeginKeys =
    [
        Localizer.Mark("Very well. The time begins now — hold that position."),
        Localizer.Mark("That will do. Your corner time has begun."),
        Localizer.Mark("Good. The clock is running — do not disappoint me.")
    ];

    public static readonly List<string> MovementDetectedKeys =
    [
        Localizer.Mark("I saw that. {0} seconds more for you."),
        Localizer.Mark("Caught you moving. Your corner grows longer by {0} seconds."),
        Localizer.Mark("That was not stillness. {0} seconds have been added.")
    ];

    public static readonly List<string> StillnessWarningKeys =
    [
        Localizer.Mark("Good. Keep it that way."),
        Localizer.Mark("Stillness suits you. Do not test my patience now."),
        Localizer.Mark("So far, so good. I am watching.")
    ];

    public static string GetRandomPosition(Random? rng = null)
    {
        return Pick(PositionKeys, rng);
    }

    public static string GetRandomBegin(Random? rng = null)
    {
        return Pick(BeginKeys, rng);
    }

    public static string GetRandomMovementDetected(Random? rng = null)
    {
        return Pick(MovementDetectedKeys, rng);
    }

    public static string GetRandomStillnessWarning(Random? rng = null)
    {
        return Pick(StillnessWarningKeys, rng);
    }

    private static string Pick(List<string> keys, Random? rng)
    {
        return keys[(rng ?? Rng).Next(keys.Count)];
    }
}
