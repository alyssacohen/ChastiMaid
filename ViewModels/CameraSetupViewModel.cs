using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class CameraSetupViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly Chore _chore;

    public string ChoreName => _chore.Name;
    public WebcamPreviewService Webcam => _mainVm.Webcam;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public CameraSetupViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Chore chore)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _chore = chore;

        _mainVm.Webcam.Start();

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
}
