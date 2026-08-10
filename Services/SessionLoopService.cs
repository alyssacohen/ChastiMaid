using ChastiMaid.Models;
using ChastiMaid.Repositories;

namespace ChastiMaid.Services;

/// <summary>
///     Manages the session game loop: periodic idle wage and random-interval check-in popups.
///     Supports pause/resume for the idle ↔ busy mode transition.
/// </summary>
public class SessionLoopService : IDisposable
{
    private readonly Random _random = new();
    private readonly SaveRepository _saveRepo;
    private readonly GameSettings _settings;

    private CancellationTokenSource? _wageCts;
    private CancellationTokenSource? _popupCts;

    private bool _missedSinceLastWage;
    private Task? _popupTask;
    private Task? _wageTask;

    public Action? OnIdleWagePaid;
    public Action? OnPopupTriggered;

    public SessionLoopService(SaveRepository saveRepo, GameSettings settings)
    {
        _saveRepo = saveRepo;
        _settings = settings;
    }

    public DateTime LastWageTime { get; private set; }

    public int WageIntervalMinutes => _settings.IdleWageIntervalMinutes;
    public double OutfitBonus { get; set; } = 1.0;

    public void Dispose()
    {
        Stop();
    }

    public void Start()
    {
        Stop();
        _missedSinceLastWage = false;
        LastWageTime = DateTime.UtcNow;
        _wageCts = new CancellationTokenSource();
        _popupCts = new CancellationTokenSource();
        _wageTask = RunWageLoop(_wageCts.Token);
        _popupTask = RunPopupLoop(_popupCts.Token);
    }

    public void Stop()
    {
        _wageCts?.Cancel();
        _wageCts?.Dispose();
        _wageCts = null;
        _wageTask = null;

        _popupCts?.Cancel();
        _popupCts?.Dispose();
        _popupCts = null;
        _popupTask = null;
    }

    /// <summary>
    ///     Pauses the wage timer (for busy mode). By default also pauses the popup timer;
    ///     pass <paramref name="pausePopups" /> = false to keep check-in popups (e.g. Hydration,
    ///     Plug and Play) running while a task is in progress.
    /// </summary>
    public void Pause(bool pausePopups = true)
    {
        _wageCts?.Cancel();
        _wageCts?.Dispose();
        _wageCts = null;
        _wageTask = null;

        if (pausePopups)
        {
            _popupCts?.Cancel();
            _popupCts?.Dispose();
            _popupCts = null;
            _popupTask = null;
        }
    }

    /// <summary>Resumes any paused timers (returning to idle mode). Resets the wage clock if it was paused.</summary>
    public void Resume()
    {
        if (_wageCts is null)
        {
            LastWageTime = DateTime.UtcNow;
            _wageCts = new CancellationTokenSource();
            _wageTask = RunWageLoop(_wageCts.Token);
        }

        if (_popupCts is null)
        {
            _popupCts = new CancellationTokenSource();
            _popupTask = RunPopupLoop(_popupCts.Token);
        }
    }

    public void ReportPopupResult(bool answered)
    {
        if (!answered)
        {
            _missedSinceLastWage = true;
        }
    }

    private async Task RunWageLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(_settings.IdleWageIntervalMinutes);
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);
            if (ct.IsCancellationRequested)
            {
                break;
            }

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
            LastWageTime = DateTime.UtcNow;
        }
    }

    private async Task RunPopupLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var delaySec = _random.Next(_settings.PopupMinIntervalSeconds, _settings.PopupMaxIntervalSeconds + 1);
            await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
            if (ct.IsCancellationRequested)
            {
                break;
            }

            OnPopupTriggered?.Invoke();
        }
    }
}
