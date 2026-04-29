using ChastiMaid.Models;

namespace ChastiMaid.Services;

/// <summary>
/// Gestisce il game loop della sessione: idle wage periodico e popup a intervalli random.
/// Supporta pausa/ripresa per la transizione idle ↔ busy mode.
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

    public SessionLoopService(SaveRepository saveRepo, GameSettings settings)
    {
        _saveRepo = saveRepo;
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        _missedSinceLastWage = false;
        _lastWageTime = DateTime.UtcNow;
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

    /// <summary>Pausa i timer (per busy mode).</summary>
    public void Pause() => Stop();

    /// <summary>Riprende i timer (ritorno a idle mode). Resetta il contatore wage.</summary>
    public void Resume()
    {
        _lastWageTime = DateTime.UtcNow;
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
                    state.CurrentDebt = Math.Max(0, state.CurrentDebt - _settings.IdleWageAmount);
                    _saveRepo.SaveGameState(state);
                }
                OnIdleWagePaid?.Invoke();
            }

            _missedSinceLastWage = false;
            _lastWageTime = DateTime.UtcNow;
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
