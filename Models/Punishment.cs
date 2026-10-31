using LiteDB;

namespace ChastiMaid.Models;

public enum PunishmentKind
{
    Cornertime,
    WritingLines,
    ForcedOutfit,
}

/// <summary>Active punishment instance inside a save slot. Fine is applied immediately and never persisted here.</summary>
public class ActivePunishment
{
    [BsonId] public int Id { get; set; }
    public PunishmentKind Kind { get; set; }
    public DateTime AssignedAt { get; set; }

    /// <summary>Fixed at assignment time and reused unchanged across retries.</summary>
    public int Severity { get; set; }

    /// <summary>Localization key for why this punishment was assigned. Null for saves predating this field.</summary>
    public string? Reason { get; set; }

    public int? CornertimeRequiredSeconds { get; set; }
    public int? CornertimeMaxSeconds { get; set; }

    public List<string>? WritingLinesSentences { get; set; }
    public int? WritingLinesTotalLines { get; set; }
    public bool WritingLinesAttemptActive { get; set; }
    public bool WritingLinesNoIdleActive { get; set; }
    public bool WritingLinesInterruptionsActive { get; set; }

    /// <summary>Current target duration, in seconds of real accredited time. Mutated in place by Soften/Aggravate.</summary>
    public double? ForcedOutfitRequiredSeconds { get; set; }

    /// <summary>Accredited progress, in seconds. Never decreases (Soften/Aggravate only rescale the target).</summary>
    public double ForcedOutfitElapsedSeconds { get; set; }

    /// <summary>
    ///     Seconds elapsed in the current continuous play stretch, reset to 0 when a new day's session start imposes
    ///     the outfit (StartSessionViewModel.Accept). The
    ///     first <c>ForcedOutfitGraceSeconds</c> of each stretch are not credited to <see cref="ForcedOutfitElapsedSeconds" />.
    /// </summary>
    public double ForcedOutfitRunElapsedSeconds { get; set; }

    /// <summary>
    ///     Baseline timestamp for the next progress tick. Null means the outfit has not been imposed yet, so no time
    ///     counts: only StartSessionViewModel.Accept sets it, when a session start imposes the outfit.
    /// </summary>
    public DateTime? ForcedOutfitLastTickUtc { get; set; }

    public List<int>? ForcedClothingItemIds { get; set; }
    public bool ForceFullOutfit { get; set; }

    /// <summary>Name of the ForcedOutfitPreset chosen at assignment time. Null for saves predating this field.</summary>
    public string? ForcedOutfitPresetName { get; set; }

    public bool IsResolved { get; set; }
}
