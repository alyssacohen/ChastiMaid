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

        var dialog = new WebcamCaptureDialogViewModel(Webcam, WebcamCaptureMode.Photo, tempPath,
            Resources.Get("SafeLockPreviewMessage"),
            Resources.Get("SafeLockNoWebcamMessage"),
            Resources.Get("SafeLockContinueWithoutSafe"), cameraIndex: _saveRepo.GetGameSettings().SelectedCameraIndex,
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
