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

    public List<string>? WritingLinesSentenceKeys { get; set; }
    public int? WritingLinesTotalLines { get; set; }
    public bool WritingLinesAttemptActive { get; set; }

    public int? ForcedOutfitRemainingSessions { get; set; }

    /// <summary>
    ///     Original total session count at assignment time, kept so Soften/Aggravate can always recompute from the
    ///     full severity rather than from whatever is left. Null for saves predating this field — treat as equal to
    ///     <see cref="ForcedOutfitRemainingSessions" /> in that case.
    /// </summary>
    public int? ForcedOutfitTotalSessions { get; set; }

    public List<int>? ForcedClothingItemIds { get; set; }
    public bool ForceFullOutfit { get; set; }

    /// <summary>Name of the ForcedOutfitPreset chosen at assignment time. Null for saves predating this field.</summary>
    public string? ForcedOutfitPresetName { get; set; }

    public bool IsResolved { get; set; }
}
