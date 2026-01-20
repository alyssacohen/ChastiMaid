using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChastiMaid.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVM;

    [ObservableProperty] private WardrobeViewModel _wardrobeVM;
    [ObservableProperty] private bool _isWardrobeOpen;

    public SettingsViewModel(MainViewModel mainVM)
    {
        _mainVM = mainVM;
        _wardrobeVM = new WardrobeViewModel(_mainVM.DbService());
    }

    [RelayCommand]
    private void OpenWardrobe() => IsWardrobeOpen = true;

    [RelayCommand]
    private void CloseWardrobe() => IsWardrobeOpen = false;

    [RelayCommand]
    private void BackToIntro() => _mainVM.NavigateTo(new IntroViewModel(_mainVM));

    [RelayCommand]
    private void StartDashboard() => _mainVM.NavigateTo(new DashboardViewModel(_mainVM.DbService()));


}

