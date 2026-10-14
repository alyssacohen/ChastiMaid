using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class SafeLockViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private ILocalizer L => _mainVm.Localizer;
    private readonly SaveRepository _saveRepo;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isDone;

    [Reactive]
    private WebcamCaptureDialogViewModel? _activeDialog;

    public SafeLockViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        Webcam = mainVm.Webcam;

        StartCameraCommand = ReactiveCommand.CreateFromTask(OnStartCamera);
        SkipCommand = ReactiveCommand.Create(StartSession);
        ContinueCommand = ReactiveCommand.Create(StartSession);
    }

    public WebcamPreviewService Webcam { get; }

    public ICommand StartCameraCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand ContinueCommand { get; }

    private async Task OnStartCamera()
    {
        var state = _saveRepo.GetGameState();
        var slotName = state?.SlotName ?? "unknown";
        var dir = AppPaths.SaveDir(slotName);
        var tempPath = Path.Combine(dir, "safe_combo_temp.png");
        var encPath = Path.Combine(dir, "safe_combo.enc");

        var dialog = new WebcamCaptureDialogViewModel(Webcam, L, WebcamCaptureMode.Photo, tempPath,
            L.T("Point the camera at the combination, then press Capture."),
            L.T("The combination photo cannot be captured. You can continue without locking the safe."),
            L.T("Continue without safe"), cameraIndex: _mainVm.Webcam.PreferredCameraIndex,
            pixelateCenterOverlay: true);
        ActiveDialog = dialog;
        var capturedPath = await dialog.Result;
        ActiveDialog = null;

        if (capturedPath is null)
        {
            StartSession();
            return;
        }

        try
        {
            CryptoService.EncryptFile(capturedPath, encPath);
            File.Delete(capturedPath);

            if (state is not null)
            {
                state.IsSafeLocked = true;
                _saveRepo.SaveGameState(state);
            }

            _mainVm.AutoSaveService.TriggerSave();
            IsDone = true;
        }
        catch
        {
            StartSession();
        }
    }

    private void StartSession()
    {
        Webcam.Stop();
        var state = _saveRepo.GetGameState();
        _saveRepo.AddSession(new Session
        {
            StartedAt = DateTime.UtcNow,
            DebtAtStart = state?.CurrentDebt ?? 0,
        });
        _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo));
    }
}
