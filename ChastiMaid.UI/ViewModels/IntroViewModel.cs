using CommunityToolkit.Mvvm.Input;

namespace ChastiMaid.UI.ViewModels;

public partial class IntroViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVM;
    public IntroViewModel(MainViewModel mainVM)
    {
        _mainVM = mainVM;
    }
    [RelayCommand] private void StartNewSession() => _mainVM.StartNewSessionCommand.Execute(null);
    [RelayCommand] private void ContinueSession() => _mainVM.ContinueSessionCommand.Execute(null);
    [RelayCommand] private void ExitApp() => _mainVM.ExitAppCommand.Execute(null);
}
