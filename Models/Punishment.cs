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

    public int? CornertimeRequiredSeconds { get; set; }
    public int? CornertimeMaxSeconds { get; set; }

    public List<string>? WritingLinesSentenceKeys { get; set; }
    public int? WritingLinesTotalLines { get; set; }
    public bool WritingLinesAttemptActive { get; set; }

    public int? ForcedOutfitRemainingSessions { get; set; }
    public List<int>? ForcedClothingItemIds { get; set; }
    public bool ForceFullOutfit { get; set; }

    public bool IsResolved { get; set; }
}
