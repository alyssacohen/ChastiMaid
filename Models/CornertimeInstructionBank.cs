namespace ChastiMaid.Models;

public static class CornertimeInstructionBank
{
    private static readonly Random Rng = new();

    public static readonly List<string> PositionKeys =
    [
        "CornertimePositionHandsBackA",
        "CornertimePositionHandsBackB",
        "CornertimePositionHandsLapA",
        "CornertimePositionHandsLapB",
        "CornertimePositionHandsNeckA",
        "CornertimePositionHandsNeckB",
        "CornertimePositionHandsKneesA",
        "CornertimePositionHandsKneesB"
    ];

    public static readonly List<string> BeginKeys =
    [
        "CornertimeBegin1",
        "CornertimeBegin2",
        "CornertimeBegin3"
    ];

    public static readonly List<string> MovementDetectedKeys =
    [
        "CornertimeMovementDetected1",
        "CornertimeMovementDetected2",
        "CornertimeMovementDetected3"
    ];

    public static readonly List<string> StillnessWarningKeys =
    [
        "CornertimeStillnessWarning1",
        "CornertimeStillnessWarning2",
        "CornertimeStillnessWarning3"
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
