 namespace ChastiMaid.Core.Models;

/// <summary>
/// Represents a complete game session including settings and current state.
/// Settings are immutable once the session is created.
/// </summary>
public class GameSession
{
    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// When this session was first created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Last time this session was played.
    /// </summary>
    public DateTime LastPlayedDate { get; set; }

    /// <summary>
    /// Immutable settings for this session. Cannot be changed during gameplay.
    /// </summary>
    public SessionSettings Settings { get; set; } = new();

    /// <summary>
    /// Current game state (level, time played, etc.).
    /// </summary>
    public GameState State { get; set; } = new();
}

/// <summary>
/// Contains all configurable settings for a game session.
/// These settings are locked once the session begins.
/// </summary>
public class SessionSettings
{
    /// <summary>
    /// Difficulty level selected for this session.
    /// </summary>
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Normal;

    /// <summary>
    /// Whether outfit regeneration is allowed during gameplay.
    /// </summary>
    public bool AllowOutfitRegeneration { get; set; } = true;

    /// <summary>
    /// Maximum number of outfit regenerations per day (0 = unlimited).
    /// </summary>
    public int MaxDailyRegenerations { get; set; } = 3;

    /// <summary>
    /// Whether the wardrobe window can be opened during gameplay.
    /// </summary>
    public bool AllowWardrobeAccess { get; set; } = true;

    // Add more settings as needed for your game
}

/// <summary>
/// Difficulty levels for the game session.
/// </summary>
public enum DifficultyLevel
{
    Easy,
    Normal,
    Hard,
}
