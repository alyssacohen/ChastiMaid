using LiteDB;

namespace ChastiMaid.Models;

/// <summary>Duration expressed in hours OR sessions, not both.</summary>
public class PunishmentDuration
{
    public int? Hours { get; set; }
    public int? Sessions { get; set; }
}

/// <summary>Configurable punishment template, set up before a game starts.</summary>
public class PunishmentTemplate
{
    [BsonId] public int Id { get; set; }
    public string Name { get; set; } = "";

    // Effects — null means the effect is inactive
    public decimal? DebtIncrease { get; set; }

    public PunishmentDuration? OutfitForceDuration { get; set; }
    public List<int>? ForcedClothingItemIds { get; set; } // required subset
    public bool ForceFullOutfit { get; set; } // true = entire outfit is fixed

    public PunishmentDuration? MultiplierPenaltyDuration { get; set; }
    public double? MultiplierPenaltyFactor { get; set; } // e.g. 0.5 halves the bonus

    public List<int>? RequiredChoreIds { get; set; } // specific chores; null = pick from list
    public int? RequiredChoreCount { get; set; } // how many random chores to assign
}

/// <summary>Active punishment instance inside a save slot.</summary>
public class ActivePunishment
{
    [BsonId] public int Id { get; set; }
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = "";
    public DateTime AssignedAt { get; set; }

    public bool DebtApplied { get; set; }

    public PunishmentDuration? RemainingOutfitForce { get; set; }
    public List<int>? ForcedClothingItemIds { get; set; }
    public bool ForceFullOutfit { get; set; }

    public PunishmentDuration? RemainingMultiplierPenalty { get; set; }
    public double? MultiplierPenaltyFactor { get; set; }

    public List<int>? PendingChoreIds { get; set; }

    public bool IsResolved { get; set; }
}
