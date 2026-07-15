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

    /// <summary>Effective multiplier applied to credits: base multiplier × bonus penalty.</summary>
    [BsonIgnore]
    public double EffectiveMultiplier => OutfitMultiplier * OutfitBonusPercent;
}
