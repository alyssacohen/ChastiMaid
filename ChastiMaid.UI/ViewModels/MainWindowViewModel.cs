using Avalonia.Threading;
using ChastiMaid.Core.Helpers;
using ChastiMaid.Core.Models;
using ChastiMaid.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;

namespace ChastiMaid.UI.ViewModels;

// Deve ereditare da ViewModelBase per funzionare con il template Avalonia 11
public partial class MainWindowViewModel : ViewModelBase
{
    private GameEngine? _gameEngine;
    private readonly DatabaseService _dbService;
    private readonly DispatcherTimer _uiTimer;

    [ObservableProperty] private string _elapsedTimeDisplay = "00:00:00";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private string _dailyOutfitDescription = "Loading...";
    [ObservableProperty] private string _playerLevelDisplay = "Level: 1";

    private int _playerLevel;

    public int PlayerLevel
    {
        get => _playerLevel;
        set
        {
            if (SetProperty(ref _playerLevel, value))
            {
                // Update the display string automatically
                PlayerLevelDisplay = $"Level: {value}";
            }
        }
    }

    private void UpdateTimerDisplay()
    {
        if (_gameEngine != null)
        {
            _gameEngine.UpdateTotalTime();
            var currentTime = _gameEngine.GetDisplayTime();
            ElapsedTimeDisplay = currentTime.ToString(@"hh\:mm\:ss");
        }
    }

    public MainWindowViewModel()
    {
        _dbService = new DatabaseService("password");
        _uiTimer = new DispatcherTimer();
        _uiTimer.Interval = TimeSpan.FromSeconds(1);
        _uiTimer.Tick += (s, e) => UpdateTimerDisplay();
        InitializeApp();
    }

    private void InitializeApp()
    {
        DataSeeder.EnsureSeedData(_dbService);
        var wardrobeService = new WardrobeService(_dbService);

        using (var db = _dbService.GetDatabase())
        {
            var stateCol = db.GetCollection<GameState>("game_state");
            var savedState = stateCol.FindAll().FirstOrDefault();

            if (savedState == null)
            {
                savedState = new GameState { TotalPlayTime = TimeSpan.Zero, Level = 1 };
            }
            _gameEngine = new GameEngine(savedState);

            PlayerLevelDisplay = $"Level: {_gameEngine.GetCurrentState().Level}";
            ElapsedTimeDisplay = _gameEngine.GetCurrentState().TotalPlayTime.ToString(@"hh\:mm\:ss");
        }

        try
        {
            var outfit = wardrobeService.GetTodaysOutfit();
            DailyOutfitDescription = FormatOutfitForDisplay(outfit);
            StatusMessage = "Welcome back!";

        }
        catch (Exception ex)
        {
            StatusMessage = "Error loading outfit: " + ex.Message;
        }
    }

    private string FormatOutfitForDisplay(Outfit outfit)
    {
        if (outfit == null || !outfit.Items.Any())
            return "No items in today's outfit.";

        // Ordiniamo gli oggetti per layer per una lettura naturale (dal basso verso l'alto)
        var sortedItems = outfit.Items
            .OrderBy(i => GetLayerOrder(i.Layers.FirstOrDefault() ?? ""));

        return string.Join("\n", sortedItems.Select(i =>
        {
            var layerName = i.Layers.FirstOrDefault()?.ToUpper() ?? "UNKNOWN";
            return $"[{layerName}] {i.Name}";
        }));
    }

    private static int GetLayerOrder(string layer)
    {
        return layer switch
        {
            "undies" => 1,
            "base" => 2,
            "outer" => 3,
            _ => 4
        };
    }

    [RelayCommand]
    private void StartActivity()
    {
        if (_gameEngine == null) return;

        StatusMessage = "Session Started!";
        _gameEngine.StartSession();
        _uiTimer.Start();
    }

    [RelayCommand]
    private void RegenerateOutfit()
    {
        var wardrobeService = new WardrobeService(_dbService);
        var newOutfit = wardrobeService.ForceRegenerate(DateTime.Today);

        DailyOutfitDescription = FormatOutfitForDisplay(newOutfit);
    }
    [RelayCommand]
    private void OpenWardrobe()
    {
        var wardrobeWindow = new Views.WardrobeWindow()
        {
            DataContext = new WardrobeViewModel(_dbService)
        };
        wardrobeWindow.Show();
    }

    public void SaveAndExit()
    {
        if (_gameEngine == null) return;
        // 1. Stop and update the final time
        _gameEngine.UpdateTotalTime();
        var state = _gameEngine.GetCurrentState();
        state.LastSessionDate = DateTime.Now;

        using (var db = _dbService.GetDatabase())
        {
            var collection = db.GetCollection<GameState>("game_state");
            collection.Upsert(state);
        }
    }
}