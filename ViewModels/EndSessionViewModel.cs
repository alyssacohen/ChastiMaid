using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public partial class EndSessionViewModel : ViewModelBase
{
    private readonly string _encPath;
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly string _slotName;

    [Reactive(SetModifier = AccessModifier.Private)]
    private Bitmap? _comboImage;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _showingCombo;

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

    public bool HasSafeCombo { get; }

    public ICommand ShowComboAndCloseCommand { get; }
    public ICommand CloseWithoutComboCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmCloseCommand { get; }

    private void RevealCombo(GameState? state)
    {
        var decrypted = CryptoService.DecryptFile(_encPath);
        var clearPath = Path.Combine(AppContext.BaseDirectory, "data", "saves", _slotName, "safe_combo.png");
        File.WriteAllBytes(clearPath, decrypted);

        using var ms = new MemoryStream(decrypted);
        ComboImage = new Bitmap(ms);
        ShowingCombo = true;

        // Mark the safe as unlocked — interest will accrue from the next sessions onward
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

        // If not revealing the combo, shut down immediately
        if (!safeUnlocked)
        {
            Shutdown();
        }
    }

    private void Cancel()
    {
        _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo));
    }

    private static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
