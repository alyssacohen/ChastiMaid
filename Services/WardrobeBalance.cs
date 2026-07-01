namespace ChastiMaid.Services;

/// <summary>
///     Single source of truth for the wardrobe weight/rejection economy and outfit-construction
///     balancing constants. Referenced by both the app (SaveRepository, StartSessionViewModel,
///     OutfitBuilder) and the offline statistical simulator (ChastiMaid.Tests/Tools).
/// </summary>
public static class WardrobeBalance
{
    /// <summary>Weight lost per item when its outfit is accepted.</summary>
    public const double AcceptWeightDecrement = 0.1;

    /// <summary>Minimum Weight an item can ever drop to on acceptance.</summary>
    public const double AcceptWeightFloor = 0.1;

    /// <summary>Weight gained per item when its outfit is rejected.</summary>
    public const double RejectWeightIncrement = 0.3;

    /// <summary>RejectionCount at or above which an item is force-seeded into a non-editable outfit.</summary>
    public const int RejectionForceThreshold = 5;

    /// <summary>Max weighted-draw attempts OutfitBuilder makes to fill a single layer.</summary>
    public const int MaxFillAttemptsPerLayer = 10;

    /// <summary>Per-layer probability (keyed by WardrobeLayer.Order) that a layer is included at all in a build.</summary>
    public static readonly IReadOnlyDictionary<int, double> LayerProbability = new Dictionary<int, double>
    {
        [0] = 1.0, // Underwear
        [1] = 0.7, // Extra
        [2] = 1.0, // Base
        [3] = 1.0, // Outerwear
        [4] = 0.5, // Accessories
    };
}
