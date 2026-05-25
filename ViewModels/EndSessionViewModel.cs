using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public class EndSessionViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly string _slotName;
    private readonly string _encPath;

    public bool HasSafeCombo { get; }

    private Bitmap? _comboImage;
    public Bitmap? ComboImage
    {
        get => _comboImage;
        private set => this.RaiseAndSetIfChanged(ref _comboImage, value);
    }

    private bool _showingCombo;
    public bool ShowingCombo
    {
        get => _showingCombo;
        private set => this.RaiseAndSetIfChanged(ref _showingCombo, value);
    }

    public ICommand ShowComboAndCloseCommand { get; }
    public ICommand CloseWithoutComboCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmCloseCommand { get; }

    public EndSessionViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var state = saveRepo.GetGameState();
        _slotName = state?.SlotName ?? "unknown";
        _encPath = Path.Combine(AppContext.BaseDirectory, "data", "saves", _slotName, "safe_combo.enc");
        HasSafeCombo = state?.IsSafeLocked == true && File.Exists(_encPath);

        ShowComboAndCloseCommand = ReactiveCommand.Create(() => RevealCombo(state));
        CloseWithoutComboCommand = ReactiveCommand.Create(() => CloseSession(false));
        CancelCommand = ReactiveCommand.Create(Cancel);
        ConfirmCloseCommand = ReactiveCommand.Create(Shutdown);
    }

    private void RevealCombo(GameState? state)
    {
        // Decripta e salva in chiaro
        var decrypted = CryptoService.DecryptFile(_encPath);
        var clearPath = Path.Combine(AppContext.BaseDirectory, "data", "saves", _slotName, "safe_combo.png");
        File.WriteAllBytes(clearPath, decrypted);

        // Mostra l'immagine
        using var ms = new MemoryStream(decrypted);
        ComboImage = new Bitmap(ms);
        ShowingCombo = true;

        // Segna il safe come aperto — gli interessi matureranno dalle prossime sessioni
        if (state is not null)
        {
            state.IsSafeLocked = false;
            _saveRepo.SaveGameState(state);
        }

        CloseSession(true);
    }

    private void CloseSession(bool safeUnlocked)
    {
        var sessions = _saveRepo.GetSessions();
        var current = sessions.LastOrDefault(s => s.EndedAt is null);
        if (current is not null)
        {
            current.EndedAt = UtcNow();
            current.DebtAtEnd = _saveRepo.GetGameState()?.CurrentDebt ?? 0;
            current.SafeUnlocked = safeUnlocked;
            _saveRepo.UpdateSession(current);
        }

        _mainVm.AutoSaveService.TriggerSave();
        _mainVm.AutoSaveService.Unbind();
        _mainVm.CloseActiveSave();

        // Se non mostra la combo, chiudi subito
        if (!safeUnlocked)
            Shutdown();
    }

    private void Cancel()
    {
        _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo));
    }

    private static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
