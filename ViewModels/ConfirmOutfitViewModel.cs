using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

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
            state.CurrentOutfitItemIds = outfit.Items.Select(i => i.Id).ToList();
            state.OutfitLockedDate = DateTime.UtcNow;
            state.OutfitBonusPercent = bonusPercent;
            state.DiaperPlayActiveThisSession = saveRepo.GetGameSettings().DiaperPlayEnabled &&
                DiaperPlayEvaluator.OutfitHasDiaperItem(state.CurrentOutfitItemIds, saveRepo.GetDiaperPlayItems());
            saveRepo.SaveGameState(state);
        }

        OutfitVm = OutfitViewModel.FromString(OutfitSummary);
        var totalItems = OutfitVm.Layers.Sum(l => l.Items.Count);
        var layersCovered = OutfitVm.Layers.Count(l => l.HasItems);
        MultiplierDisplay = $"×{bonusPercent * outfit.TotalMultiplier:F2}";
        ItemCountDisplay = string.Format(Resources.Get("ConfirmOutfitItemCountFormat"), totalItems, layersCovered);
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
        ItemCountDisplay = string.Format(Resources.Get("ConfirmOutfitItemCountFormat"), totalItems, layersCovered);
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
        var dir = AppPaths.SaveDir(slotName);
        var videoPath = Path.Combine(dir, "outfit.avi");

        var dialog = new WebcamCaptureDialogViewModel(Webcam, WebcamCaptureMode.Video, videoPath,
            Resources.Get("ConfirmOutfitPreviewMessage"),
            Resources.Get("ConfirmOutfitNoWebcamMessage"),
            cameraIndex: _mainVm.Webcam.PreferredCameraIndex);
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
                StartedAt = DateTime.UtcNow,
                DebtAtStart = state.CurrentDebt,
            });
            _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo));
        }
        else
        {
            _mainVm.NavigateTo(new SafeLockViewModel(_mainVm, _saveRepo));
        }
    }
}
