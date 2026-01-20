using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using ChastiMaid.Core.Services;

namespace ChastiMaid.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _showConfirmOverlay;

    private readonly DatabaseService _dbService;
    private readonly IntroViewModel _introVM;
    private readonly SettingsViewModel _settingsVM;
    private readonly DashboardViewModel _dashboardVM;

    public MainViewModel()
    {
        _dbService = new DatabaseService("password");
        // Start page
        _currentPage = new IntroViewModel(this);
    }

    public DatabaseService DbService() => _dbService;

    public void NavigateTo(ViewModelBase nextPage) => CurrentPage = nextPage;

    [RelayCommand]
    public void StartNewSession()
    {
        ShowConfirmOverlay = true;
    }

    [RelayCommand]
    public void ConfirmNewSession()
    {
        ShowConfirmOverlay = false;
        // TODO - Add logic to reset the session without losing wardrobe and initial settings
        NavigateTo(new SettingsViewModel(this));
    }

    [RelayCommand]
    public void CancelNewSession()
    {
        ShowConfirmOverlay = false;
    }

    [RelayCommand] public void ContinueSession() => NavigateTo(new DashboardViewModel(_dbService));

    [RelayCommand] public void ExitApp() => Environment.Exit(0);
}
