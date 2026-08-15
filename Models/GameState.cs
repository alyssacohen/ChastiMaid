using LiteDB;

namespace ChastiMaid.Models;

public class GameState
{
    [BsonId] public int Id { get; set; } = 1; // one instance per save slot
    public string SlotName { get; set; } = "";
    public decimal InitialDebt { get; set; }
    public decimal CurrentDebt { get; set; }
    public double DailyInterestRate { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastSessionAt { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsSafeLocked { get; set; }
    public string? OutfitVideoPath { get; set; }
    public string? CurrentOutfit { get; set; }
    public List<int>? CurrentOutfitItemIds { get; set; }

    // Outfit lock: once set, outfit cannot be changed until the next day
    public DateTime? OutfitLockedDate { get; set; }
    public double OutfitBonusPercent { get; set; } = 1.0;
    public double OutfitMultiplier { get; set; } = 1.0;

    // Set at EndSession if a Plug & Play accessory was still worn; consumed once by the next
    // StartSessionViewModel outfit generation (seed item + resumed countdown), then cleared.
    public int? PendingPlugAndPlaySeedItemId { get; set; }
    public double? PendingPlugAndPlayRemainingMinutes { get; set; }

    // Set at outfit confirmation (new game or outfit change) if DiaperPlayEnabled is on and the
    // confirmed outfit contains an item tagged "diaper". Not re-evaluated until the next outfit
    // confirmation.
    public bool DiaperPlayActiveThisSession { get; set; }

    // The chastity device currently locked on. Persists across sessions regardless of the daily
    // outfit lock; only re-rolled when the safe was opened at the previous session's close
    // (IsSafeLocked == false).
    public int? CurrentChastityDeviceItemId { get; set; }

    /// <summary>Sum of all wardrobe item Weight values at new-game snapshot time — the target the periodic rescale restores.</summary>
    public double InitialWardrobeWeightTotal { get; set; }

    /// <summary>Aggregate RejectItems calls since the last rescale; triggers WardrobeBalance.RescaleEveryNRejections.</summary>
    public int WardrobeRejectionsSinceRescale { get; set; }

    /// <summary>Effective multiplier applied to credits: base multiplier × bonus penalty.</summary>
    [BsonIgnore]
    public double EffectiveMultiplier => OutfitMultiplier * OutfitBonusPercent;

    /// <summary>Reusable flag for a future permission-request system: toilet access is always denied
    /// while Diaper Play is active for the session.</summary>
    [BsonIgnore]
    public bool IsToiletPermissionAllowed => !DiaperPlayActiveThisSession;
}
