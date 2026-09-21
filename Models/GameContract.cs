namespace ChastiMaid.Models;

/// <summary>
///     Everything a new game is created from: settings plus every profile collection that a save snapshots
///     (wardrobe, outfit rules, tasks, punishment and permission configuration). Serialized as a
///     <c>.contract.json</c> file. Machine-specific and personal settings are excluded (see
///     <see cref="GameSettings" />) and the file carries no acceptance information or progress.
///     Sections are nullable so a file that omits one can be told apart from one that has it empty.
/// </summary>
public class GameContract
{
    /// <summary>The schema version this build reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>File-format version; a file with a different value is rejected untouched.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Game-rule settings (device-level properties are not serialized).</summary>
    public GameSettings? Settings { get; set; }

    /// <summary>Wardrobe items.</summary>
    public List<ClothingItem>? Wardrobe { get; set; }

    /// <summary>Outfit rules, validated against the wardrobe tags.</summary>
    public List<OutfitRule>? OutfitRules { get; set; }

    /// <summary>Recurring tasks (chores).</summary>
    public List<Chore>? Tasks { get; set; }

    /// <summary>Forced Outfit punishment presets; their item ids refer to <see cref="Wardrobe" />.</summary>
    public List<ForcedOutfitPreset>? ForcedOutfitPresets { get; set; }

    /// <summary>Wardrobe items eligible for Plug and Play, with their duration range.</summary>
    public List<PlugAndPlayItemConfig>? PlugAndPlayItems { get; set; }

    /// <summary>Wardrobe items that count as diapers for Diaper Play.</summary>
    public List<DiaperPlayItemConfig>? DiaperPlayItems { get; set; }

    /// <summary>Wardrobe items that count as chastity devices.</summary>
    public List<ChastityDeviceItemConfig>? ChastityDeviceItems { get; set; }

    /// <summary>Per-kind permission tuning (grant chance, duration, cooldown).</summary>
    public List<PermissionTypeConfig>? PermissionTypes { get; set; }
}
