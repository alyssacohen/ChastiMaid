using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class CameraSetupViewModel : ViewModelBase
{
    private readonly Chore _chore;
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;

    public CameraSetupViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Chore chore)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _chore = chore;

        var cameraIndex = mainVm.Webcam.PreferredCameraIndex;
        _mainVm.Webcam.Start(cameraIndex);
        _ = _mainVm.Webcam.RefreshAvailableCamerasAsync(cameraIndex);

        ConfirmCommand = ReactiveCommand.Create(() =>
        {
            _mainVm.Webcam.Stop();
            _mainVm.NavigateTo(new DashboardViewModel(mainVm, saveRepo, chore));
        });

        CancelCommand = ReactiveCommand.Create(() =>
        {
            _mainVm.Webcam.Stop();
            _mainVm.NavigateTo(new DashboardViewModel(mainVm, saveRepo));
        });
    }

    public string ChoreName => _chore.Name;
    public WebcamPreviewService Webcam => _mainVm.Webcam;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
}
