using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public partial class SafeLockViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly WebcamPreviewService _webcam;

    public WebcamPreviewService Webcam => _webcam;

    [Reactive(SetModifier = AccessModifier.Private)] private bool _isPreviewing;
    [Reactive(SetModifier = AccessModifier.Private)] private bool _noWebcam;
    [Reactive(SetModifier = AccessModifier.Private)] private bool _isDone;

    public bool IsInitial => !IsPreviewing && !NoWebcam && !IsDone;

    public ICommand StartCameraCommand { get; }
    public ICommand CaptureCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand ContinueCommand { get; }

    public SafeLockViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _webcam = mainVm.Webcam;

        StartCameraCommand = ReactiveCommand.Create(OnStartCamera);
        CaptureCommand = ReactiveCommand.CreateFromTask(CapturePhoto);
        SkipCommand = ReactiveCommand.Create(StartSession);
        ContinueCommand = ReactiveCommand.Create(StartSession);
    }

    private void OnStartCamera()
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

    private async Task CapturePhoto()
    {
        var state = _saveRepo.GetGameState();
        var slotName = state?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);
        var tempPath = Path.Combine(dir, "safe_combo_temp.png");
        var encPath = Path.Combine(dir, "safe_combo.enc");

        try
        {
            if (!await _webcam.CaptureFrameAsync(tempPath))
            {
                SetNoWebcam();
                return;
            }

            CryptoService.EncryptFile(tempPath, encPath);
            File.Delete(tempPath);

            if (state is not null)
            {
                state.IsSafeLocked = true;
                _saveRepo.SaveGameState(state);
            }

            _mainVm.AutoSaveService.TriggerSave();
        }
        catch
        {
            SetNoWebcam();
            return;
        }

        _webcam.Stop();
        IsPreviewing = false;
        IsDone = true;
    }

    private void SetNoWebcam()
    {
        _webcam.Stop();
        IsPreviewing = false;
        NoWebcam = true;
    }

    private void StartSession()
    {
        _webcam.Stop();
        var state = _saveRepo.GetGameState();
        _saveRepo.AddSession(new Session
        {
            StartedAt = UtcNow(),
            DebtAtStart = state?.CurrentDebt ?? 0,
            IsTestSession = Program.TestMode,
        });
        _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo));
    }
}
