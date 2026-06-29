using ChastiMaid.Repositories;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.Services;

/// <summary>
///     Auto-save service: exposes TriggerSave() for point-in-time saves
///     and a configurable periodic timer. Call StartTimer() from the dashboard
///     and StopTimer() when leaving the session.
/// </summary>
public class AutoSaveService : IDisposable
{
    private CancellationTokenSource? _cts;
    private SaveRepository? _saveRepo;
    private Task? _timerTask;

    public void Dispose()
    {
        TriggerSave();
        StopTimer();
    }

    /// <summary>Sets the active repository. Call when loading or creating a save slot.</summary>
    public void Bind(SaveRepository saveRepo)
    {
        _saveRepo = saveRepo;
    }

    /// <summary>Unbinds the repository. Call before closing the save DB.</summary>
    public void Unbind()
    {
        StopTimer();
        _saveRepo = null;
    }

    /// <summary>Saves the current state. Safe to call from anywhere.</summary>
    public void TriggerSave()
    {
        if (_saveRepo is null)
        {
            return;
        }

        try
        {
            var state = _saveRepo.GetGameState();
            if (state is null)
            {
                return;
            }

            state.LastSessionAt = UtcNow();
            _saveRepo.SaveGameState(state);
        }
        catch (ObjectDisposedException)
        {
            /* DB already closed */
        }
    }

    /// <summary>Starts the periodic timer. If already running, restarts it with the new interval.</summary>
    public void StartTimer(int intervalMinutes)
    {
        StopTimer();
        if (intervalMinutes <= 0)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        _timerTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(token))
            {
                TriggerSave();
            }
        }, token);
    }

    /// <summary>Stops the periodic timer.</summary>
    public void StopTimer()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _timerTask = null;
    }
}
