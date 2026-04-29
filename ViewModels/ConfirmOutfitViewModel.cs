using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class ConfirmOutfitViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly WebcamPreviewService _webcam;

    public string OutfitSummary { get; }
    public WebcamPreviewService Webcam => _webcam;

    private bool _isPreviewing;
    public bool IsPreviewing { get => _isPreviewing; private set => this.RaiseAndSetIfChanged(ref _isPreviewing, value); }

    private bool _isRecording;
    public bool IsRecording { get => _isRecording; private set => this.RaiseAndSetIfChanged(ref _isRecording, value); }

    private bool _noWebcam;
    public bool NoWebcam { get => _noWebcam; private set => this.RaiseAndSetIfChanged(ref _noWebcam, value); }

    private int _countdown;
    public int Countdown { get => _countdown; private set => this.RaiseAndSetIfChanged(ref _countdown, value); }

    public bool IsInitial => !IsPreviewing && !IsRecording && !NoWebcam;

    public ICommand ConfirmOutfitCommand { get; }
    public ICommand RecordCommand { get; }
    public ICommand SkipCommand { get; }

    public ConfirmOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit outfit)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _webcam = mainVm.Webcam;
        OutfitSummary = outfit.ToString();

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
        catch { /* Registrazione fallita — procedi comunque */ }

        NavigateToSafeLock();
    }

    private void NavigateToSafeLock()
    {
        _webcam.Stop();
        _mainVm.NavigateTo(new SafeLockViewModel(_mainVm, _saveRepo));
    }
}
