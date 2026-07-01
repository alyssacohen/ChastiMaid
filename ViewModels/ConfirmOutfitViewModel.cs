using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public partial class ConfirmOutfitViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;

    [Reactive]
    private WebcamCaptureDialogViewModel? _activeDialog;

    public ConfirmOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit outfit,
        double bonusPercent = 1.0)
        : this(mainVm, saveRepo, bonusPercent)
    {
        OutfitSummary = outfit.ToString();

        var state = saveRepo.GetGameState();
        if (state is not null)
        {
            state.CurrentOutfit = OutfitSummary;
            state.OutfitLockedDate = UtcNow();
            state.OutfitBonusPercent = bonusPercent;
            saveRepo.SaveGameState(state);
        }

        OutfitVm = OutfitViewModel.FromString(OutfitSummary);
        var totalItems = OutfitVm.Layers.Sum(l => l.Items.Count);
        var layersCovered = OutfitVm.Layers.Count(l => l.HasItems);
        MultiplierDisplay = $"×{bonusPercent * outfit.TotalMultiplier:F2}";
        ItemCountDisplay = $"{totalItems} items · {layersCovered} layers covered";
    }

    /// <summary>Used when outfit is already locked — reads summary from saved state.</summary>
    public ConfirmOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, double bonusPercent)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        Webcam = mainVm.Webcam;

        var state = saveRepo.GetGameState();
        OutfitSummary = state?.CurrentOutfit ?? "";

        ConfirmOutfitCommand = ReactiveCommand.CreateFromTask(OnOutfitConfirmed);

        OutfitVm = OutfitViewModel.FromString(OutfitSummary);
        var totalItems = OutfitVm.Layers.Sum(l => l.Items.Count);
        var layersCovered = OutfitVm.Layers.Count(l => l.HasItems);
        MultiplierDisplay = $"×{state?.EffectiveMultiplier ?? 1.0:F2}";
        ItemCountDisplay = $"{totalItems} items · {layersCovered} layers covered";
    }

    public string OutfitSummary { get; }
    public OutfitViewModel OutfitVm { get; private set; } = new([]);
    public string MultiplierDisplay { get; private set; } = "×1.00";
    public string ItemCountDisplay { get; private set; } = "";
    public WebcamPreviewService Webcam { get; }

    public ICommand ConfirmOutfitCommand { get; }

    private async Task OnOutfitConfirmed()
    {
        var state = _saveRepo.GetGameState();
        var slotName = state?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);
        var videoPath = Path.Combine(dir, "outfit.avi");

        var dialog = new WebcamCaptureDialogViewModel(Webcam, WebcamCaptureMode.Video, videoPath,
            "Check yourself in the preview, then press Record.",
            "The outfit video cannot be recorded. You can skip this step and continue.");
        ActiveDialog = dialog;
        var recordedPath = await dialog.Result;
        ActiveDialog = null;

        if (recordedPath is not null && state is not null)
        {
            state.OutfitVideoPath = recordedPath;
            _saveRepo.SaveGameState(state);
        }

        NavigateToSafeLock();
    }

    private void NavigateToSafeLock()
    {
        var state = _saveRepo.GetGameState();
        if (state?.IsSafeLocked == true)
        {
            // Safe already locked — skip SafeLock, create session, and navigate to dashboard
            _saveRepo.AddSession(new Session
            {
                StartedAt = UtcNow(),
                DebtAtStart = state.CurrentDebt,
                IsTestSession = Program.TestMode,
            });
            _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo));
        }
        else
        {
            _mainVm.NavigateTo(new SafeLockViewModel(_mainVm, _saveRepo));
        }
    }
}
