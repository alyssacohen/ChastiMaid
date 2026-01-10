using ChastiMaid.Core.Models;
using System.Diagnostics;

namespace ChastiMaid.Core.Services;

/// <summary>
/// Handles game session timing and progression logic.
/// </summary>
public class GameEngine
{
    private readonly Stopwatch _sessionTimer;
    private readonly GameState _currentState;

    public GameEngine(GameState loadedState)
    {
        _currentState = loadedState ?? new GameState { TotalPlayTime = TimeSpan.Zero, Level = 1 };
        _sessionTimer = new Stopwatch();
    }

    public void StartSession()
    {
        if (!_sessionTimer.IsRunning)
            _sessionTimer.Start();
    }
    public TimeSpan GetDisplayTime()
    {
        return _currentState.TotalPlayTime + _sessionTimer.Elapsed;
    }

    /// <summary>
    /// Stops the timer and updates the total play time in the state.
    /// </summary>
    public void UpdateTotalTime()
    {
        if (_sessionTimer.IsRunning)
        {
            _currentState.TotalPlayTime += _sessionTimer.Elapsed;
            _sessionTimer.Restart();
        }
    }

    public GameState GetCurrentState() => _currentState;
}