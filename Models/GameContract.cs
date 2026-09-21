namespace ChastiMaid.Models;

/// <summary>
///     The game rules a new game is played under: game-rule settings plus the wardrobe, outfit rules and
///     tasks that define it. Serialized as a <c>.contract.json</c> file. Device-level and personal settings
///     are excluded (see <see cref="GameSettings" />) and the file carries no acceptance information.
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
}
