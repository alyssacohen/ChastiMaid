using ChastiMaid.Core.Models;

namespace ChastiMaid.Core.Services;

/// <summary>
/// Manages game session lifecycle including creation, loading, and persistence.
/// Each session has immutable settings that cannot be changed during gameplay.
/// </summary>

public class SessionManager
{
    private readonly DatabaseService _dbService;
    private GameSession? _currentSession;

    public SessionManager(DatabaseService dbService)
    {
        _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
    }

    /// <summary>
    /// Gets the currently active session, if any.
    /// </summary>
    public GameSession? CurrentSession => _currentSession;

    /// <summary>
    /// Checks if there is an existing saved session.
    /// </summary>
    public bool HasExistingSession()
    {
        using var db = _dbService.GetDatabase();
        var sessionCol = db.GetCollection<GameSession>("sessions");
        return sessionCol.FindAll().Any();
    }

    /// <summary>
    /// Loads the most recent session from the database.
    /// </summary>
    /// <returns>True if a session was successfully loaded, false otherwise.</returns>
    public bool LoadSession()
    {
        using var db = _dbService.GetDatabase();
        var sessionCol = db.GetCollection<GameSession>("sessions");

        // Load the most recent session (you might want to add multiple save slots later)
        _currentSession = sessionCol.FindAll()
            .OrderByDescending(s => s.LastPlayedDate)
            .FirstOrDefault();

        return _currentSession != null;
    }

    /// <summary>
    /// Creates a new game session with the provided settings.
    /// Settings are immutable once the session is created.
    /// </summary>
    public void CreateNewSession(SessionSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        _currentSession = new GameSession
        {
            Id = Guid.NewGuid().ToString(),
            CreatedDate = DateTime.Now,
            LastPlayedDate = DateTime.Now,
            Settings = settings,
            State = new GameState
            {
                TotalPlayTime = TimeSpan.Zero,
                Level = 1,
                LastSessionDate = DateTime.Now
            }
        };

        SaveSession();
    }

    /// <summary>
    /// Saves the current session to the database.
    /// </summary>
    public void SaveSession()
    {
        if (_currentSession == null)
            throw new InvalidOperationException("No active session to save.");

        _currentSession.LastPlayedDate = DateTime.Now;

        using var db = _dbService.GetDatabase();
        var sessionCol = db.GetCollection<GameSession>("sessions");
        sessionCol.Upsert(_currentSession);
    }

    /// <summary>
    /// Updates the game state within the current session.
    /// </summary>
    public void UpdateGameState(GameState state)
    {
        if (_currentSession == null)
            throw new InvalidOperationException("No active session.");

        _currentSession.State = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>
    /// Deletes all existing sessions. Use with caution.
    /// </summary>
    public void DeleteAllSessions()
    {
        using var db = _dbService.GetDatabase();
        var sessionCol = db.GetCollection<GameSession>("sessions");
        sessionCol.DeleteAll();
        _currentSession = null;
    }

    /// <summary>
    /// Closes the current session and performs cleanup.
    /// </summary>
    public void CloseSession()
    {
        if (_currentSession != null)
        {
            SaveSession();
            _currentSession = null;
        }
    }
}

