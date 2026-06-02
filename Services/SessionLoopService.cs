using ChastiMaid.Models;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.Services;

/// <summary>
/// Manages the session game loop: periodic idle wage and random-interval check-in popups.
/// Supports pause/resume for the idle ↔ busy mode transition.
/// </summary>
public class SessionLoopService : IDisposable
{
    private readonly SaveRepository _saveRepo;
    private readonly GameSettings _settings;
    private readonly Random _random = new();

    private CancellationTokenSource? _cts;
    private Task? _wageTask;
    private Task? _popupTask;

    private bool _missedSinceLastWage;
    private DateTime _lastWageTime;

    public Action? OnIdleWagePaid;
    public Action? OnPopupTriggered;

    public DateTime LastWageTime => _lastWageTime;
    public int WageIntervalMinutes => _settings.IdleWageIntervalMinutes;
    public double OutfitBonus { get; set; } = 1.0;

    public SessionLoopService(SaveRepository saveRepo, GameSettings settings)
    {
        _saveRepo = saveRepo;
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        _missedSinceLastWage = false;
        _lastWageTime = UtcNow();
        _cts = new CancellationTokenSource();
        _wageTask = RunWageLoop(_cts.Token);
        _popupTask = RunPopupLoop(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _wageTask = null;
        _popupTask = null;
    }

    /// <summary>Pauses the timers (for busy mode).</summary>
    public void Pause() => Stop();

    /// <summary>Resumes the timers (returning to idle mode). Resets the wage clock.</summary>
    public void Resume()
    {
        _lastWageTime = UtcNow();
        _cts = new CancellationTokenSource();
        _wageTask = RunWageLoop(_cts.Token);
        _popupTask = RunPopupLoop(_cts.Token);
    }

    public void ReportPopupResult(bool answered)
    {
        if (!answered)
            _missedSinceLastWage = true;
    }

    private async Task RunWageLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(_settings.IdleWageIntervalMinutes);
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);
            if (ct.IsCancellationRequested) break;

            if (!_missedSinceLastWage)
            {
                var state = _saveRepo.GetGameState();
                if (state is not null)
                {
                    var wage = Math.Round(_settings.IdleWageAmount * (decimal)OutfitBonus, 0);
                    state.CurrentDebt = Math.Max(0, state.CurrentDebt - wage);
                    _saveRepo.SaveGameState(state);
                }
                OnIdleWagePaid?.Invoke();
            }

            _missedSinceLastWage = false;
            _lastWageTime = UtcNow();
        }
    }

    private async Task RunPopupLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var delaySec = _random.Next(_settings.PopupMinIntervalSeconds, _settings.PopupMaxIntervalSeconds + 1);
            await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
            if (ct.IsCancellationRequested) break;
            OnPopupTriggered?.Invoke();
        }
    }

    public void Dispose() => Stop();
}
