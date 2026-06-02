using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public partial class ConfirmOutfitViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly WebcamPreviewService _webcam;

    public string OutfitSummary { get; }
    public WebcamPreviewService Webcam => _webcam;

    [Reactive(SetModifier = AccessModifier.Private)] private bool _isPreviewing;
    [Reactive(SetModifier = AccessModifier.Private)] private bool _isRecording;
    [Reactive(SetModifier = AccessModifier.Private)] private bool _noWebcam;
    [Reactive(SetModifier = AccessModifier.Private)] private int _countdown;

    public bool IsInitial => !IsPreviewing && !IsRecording && !NoWebcam;

    public ICommand ConfirmOutfitCommand { get; }
    public ICommand RecordCommand { get; }
    public ICommand SkipCommand { get; }

    public ConfirmOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit outfit, double bonusPercent = 1.0)
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
    }

    /// <summary>Used when outfit is already locked — reads summary from saved state.</summary>
    public ConfirmOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, double bonusPercent)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _webcam = mainVm.Webcam;

        var state = saveRepo.GetGameState();
        OutfitSummary = state?.CurrentOutfit ?? "";

        ConfirmOutfitCommand = ReactiveCommand.Create(OnOutfitConfirmed);
        RecordCommand = ReactiveCommand.CreateFromTask(OnRecord);
        SkipCommand = ReactiveCommand.Create(NavigateToSafeLock);
    }

    private void OnOutfitConfirmed()
    {
        if (!_webcam.Start())
        {
            NoWebcam = true;
            this.RaisePropertyChanged(nameof(IsInitial));
            return;
        }

        IsPreviewing = true;
        this.RaisePropertyChanged(nameof(IsInitial));
    }

    private async Task OnRecord()
    {
        IsRecording = true;
        IsPreviewing = false;
        Countdown = 5;

        var state = _saveRepo.GetGameState();
        var slotName = state?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);
        var videoPath = Path.Combine(dir, "outfit.avi");

        try
        {
            await _webcam.RecordVideoAsync(videoPath, 5, new Progress<int>(r => Countdown = r));

            if (state is not null)
            {
                state.OutfitVideoPath = videoPath;
                _saveRepo.SaveGameState(state);
            }
        }
        catch { /* Recording failed — proceed anyway */ }

        NavigateToSafeLock();
    }

    private void NavigateToSafeLock()
    {
        _webcam.Stop();

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
