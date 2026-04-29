namespace ChastiMaid.Services;

/// <summary>
/// Salvataggio automatico: espone TriggerSave() per eventi puntuali
/// e un timer periodico configurabile. Chiamare StartTimer() dalla dashboard
/// e StopTimer() quando si esce dalla sessione.
/// </summary>
public class AutoSaveService : IDisposable
{
    private SaveRepository? _saveRepo;
    private CancellationTokenSource? _cts;
    private Task? _timerTask;

    /// <summary>Imposta il repository attivo. Chiamare quando si carica/crea uno slot.</summary>
    public void Bind(SaveRepository saveRepo) => _saveRepo = saveRepo;

    /// <summary>Salva lo stato corrente. Sicuro da chiamare da qualsiasi punto.</summary>
    public void TriggerSave()
    {
        if (_saveRepo is null) return;
        var state = _saveRepo.GetGameState();
        if (state is null) return;
        state.LastSessionAt = DateTime.UtcNow;
        _saveRepo.SaveGameState(state);
    }

    /// <summary>Avvia il timer periodico. Se già attivo, lo riavvia con il nuovo intervallo.</summary>
    public void StartTimer(int intervalMinutes)
    {
        StopTimer();
        if (intervalMinutes <= 0) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        _timerTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(token))
                TriggerSave();
        }, token);
    }

    /// <summary>Ferma il timer periodico.</summary>
    public void StopTimer()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _timerTask = null;
    }

    public void Dispose()
    {
        TriggerSave();
        StopTimer();
    }
}
