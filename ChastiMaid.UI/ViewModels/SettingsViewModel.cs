using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Reactive;
using System.Collections.ObjectModel;
using ChastiMaid.Core.Services;
using ChastiMaid.Core.Models;

namespace ChastiMaid.UI.ViewModels;

/// <summary>
/// View model for configuring a new game session.
/// Settings configured here are locked once the session begins.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVM;
    private readonly DatabaseService _dbService;
    private readonly SessionManager _sessionManager;

    [Reactive] private DifficultyLevel _selectedDifficulty = DifficultyLevel.Normal;
    [Reactive] private bool _allowOutfitRegeneration = true;
    [Reactive] private int _maxDailyRegenerations = 3;
    [Reactive] private bool _allowWardrobeAccess = true;

    public SettingsViewModel(MainViewModel mainVM, DatabaseService dbService, SessionManager sessionManager)
    {
        _mainVM = mainVM;
        _dbService = dbService;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Available difficulty levels for selection.
    /// </summary>
    public ObservableCollection<DifficultyLevel> DifficultyLevels { get; } = new()
    {
        DifficultyLevel.Easy,
        DifficultyLevel.Normal,
        DifficultyLevel.Hard
    };

    [ReactiveCommand]
    private void StartSession()
    {
        // Create session settings from current configuration
        var settings = new SessionSettings
        {
            Difficulty = SelectedDifficulty,
            AllowOutfitRegeneration = AllowOutfitRegeneration,
            MaxDailyRegenerations = MaxDailyRegenerations,
            AllowWardrobeAccess = AllowWardrobeAccess
        };

        // Create new session with these settings
        _sessionManager.CreateNewSession(settings);

        // Navigate to dashboard
        _mainVM.NavigateTo(new DashboardViewModel(_mainVM, _dbService, _sessionManager));
    }

    [ReactiveCommand]
    private void Cancel()
    {
        // Return to intro screen
        _mainVM.NavigateTo(new IntroViewModel(_mainVM, _sessionManager));
    }
}
