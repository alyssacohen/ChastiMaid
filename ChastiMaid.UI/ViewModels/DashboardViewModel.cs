using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Linq;
using System.Reactive;
using ChastiMaid.Core.Helpers;
using ChastiMaid.Core.Models;
using ChastiMaid.Core.Services;

namespace ChastiMaid.UI.ViewModels;

/// <summary>
/// View model for the main dashboard where gameplay occurs.
/// Displays session info, outfit, timer, and provides gameplay actions.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly MainViewModel _mainVM;
    private readonly DatabaseService _dbService;
    private readonly SessionManager _sessionManager;
    private readonly WardrobeService _wardrobeService;
    private GameEngine? _gameEngine;
    private readonly DispatcherTimer _uiTimer;
    private int _regenerationCount = 0;

    [Reactive] private string _elapsedTimeDisplay = "00:00:00";
    [Reactive] private string _statusMessage = "Ready";
    [Reactive] private string _dailyOutfitDescription = "Loading...";
    [Reactive] private string _playerLevelDisplay = "Level: 1";
    [Reactive] private string _sessionInfoDisplay = "";
    [Reactive] private bool _canRegenerate = true;
    [Reactive] private bool _canOpenWardrobe = true;

    public DashboardViewModel(MainViewModel mainVM, DatabaseService dbService, SessionManager sessionManager)
    {
        _mainVM = mainVM;
        _dbService = dbService;
        _sessionManager = sessionManager;
        _wardrobeService = new WardrobeService(_dbService);

        _uiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiTimer.Tick += (s, e) => UpdateTimerDisplay();

        // Initialize conditional commands
        InitializeConditionalCommands();

        InitializeSession();
    }

    /// <summary>
    /// Initializes commands that require WhenAnyValue conditions.
    /// The [ReactiveCommand] attribute doesn't support these yet.
    /// </summary>
    private void InitializeConditionalCommands()
    {
        RegenerateOutfitCommand = ReactiveCommand.Create(
            RegenerateOutfit,
            this.WhenAnyValue(x => x.CanRegenerate)
        );

        OpenWardrobeCommand = ReactiveCommand.Create(
            OpenWardrobe,
            this.WhenAnyValue(x => x.CanOpenWardrobe)
        );
    }

    // Commands with conditions (manual initialization required)
    public ReactiveCommand<Unit, Unit> RegenerateOutfitCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> OpenWardrobeCommand { get; private set; }

    /// <summary>
    /// Initializes the dashboard with the current session data.
    /// </summary>
    private void InitializeSession()
    {
        var session = _sessionManager.CurrentSession;
        if (session == null)
        {
            StatusMessage = "Error: No active session";
            return;
        }

        // Seed data if needed
        DataSeeder.EnsureSeedData(_dbService);

        // Initialize game engine with session state
        _gameEngine = new GameEngine(session.State);

        // Update UI with session info
        UpdateSessionInfo(session);

        // Apply session settings
        CanRegenerate = session.Settings.AllowOutfitRegeneration;
        CanOpenWardrobe = session.Settings.AllowWardrobeAccess;

        // Load today's outfit
        LoadTodaysOutfit();

        StatusMessage = "Welcome back!";
    }

    /// <summary>
    /// Updates the session info display with difficulty and settings.
    /// </summary>
    private void UpdateSessionInfo(GameSession session)
    {
        PlayerLevelDisplay = $"Level: {session.State.Level}";
        ElapsedTimeDisplay = session.State.TotalPlayTime.ToString(@"hh\:mm\:ss");
        SessionInfoDisplay = $"Difficulty: {session.Settings.Difficulty}";
    }

    /// <summary>
    /// Loads and displays today's outfit.
    /// </summary>
    private void LoadTodaysOutfit()
    {
        try
        {
            var outfit = _wardrobeService.GetTodaysOutfit();
            DailyOutfitDescription = FormatOutfitForDisplay(outfit);
        }
        catch (Exception ex)
        {
            StatusMessage = "Error loading outfit: " + ex.Message;
        }
    }

    /// <summary>
    /// Formats outfit items for display, ordered by layer.
    /// </summary>
    private string FormatOutfitForDisplay(Outfit outfit)
    {
        if (outfit == null || !outfit.Items.Any())
            return "No items in today's outfit.";

        // Order items by layer for natural reading (bottom to top)
        var sortedItems = outfit.Items
            .OrderBy(i => GetLayerOrder(i.Layers.FirstOrDefault() ?? ""));

        return string.Join("\n", sortedItems.Select(i =>
        {
            var layerName = i.Layers.FirstOrDefault()?.ToUpper() ?? "UNKNOWN";
            return $"[{layerName}] {i.Name}";
        }));
    }

    /// <summary>
    /// Determines the display order for clothing layers.
    /// </summary>
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

    /// <summary>
    /// Updates the timer display with current session time.
    /// </summary>
    private void UpdateTimerDisplay()
    {
        if (_gameEngine != null)
        {
            _gameEngine.UpdateTotalTime();
            var currentTime = _gameEngine.GetDisplayTime();
            ElapsedTimeDisplay = currentTime.ToString(@"hh\:mm\:ss");
        }
    }

    [ReactiveCommand]
    private void StartActivity()
    {
        if (_gameEngine == null) return;

        StatusMessage = "Session Started!";
        _gameEngine.StartSession();
        _uiTimer.Start();
    }

    private void RegenerateOutfit()
    {
        var session = _sessionManager.CurrentSession;
        if (session == null || !CanRegenerate) return;

        // Check regeneration limit
        if (session.Settings.MaxDailyRegenerations > 0)
        {
            if (_regenerationCount >= session.Settings.MaxDailyRegenerations)
            {
                StatusMessage = "Daily regeneration limit reached!";
                return;
            }
            _regenerationCount++;
        }

        var newOutfit = _wardrobeService.ForceRegenerate(DateTime.Today);
        DailyOutfitDescription = FormatOutfitForDisplay(newOutfit);

        StatusMessage = $"Outfit regenerated! ({_regenerationCount}/{session.Settings.MaxDailyRegenerations})";
    }

    private void OpenWardrobe()
    {
        if (!CanOpenWardrobe) return;

        // Navigate to wardrobe view with navigation context
        _mainVM.NavigateTo(new WardrobeViewModel(_dbService, _mainVM, _sessionManager));
    }

    [ReactiveCommand]
    private void ReturnToMenu()
    {
        SaveAndExit();
        _mainVM.NavigateTo(new IntroViewModel(_mainVM, _sessionManager));
    }

    /// <summary>
    /// Saves the current session state before exiting.
    /// </summary>
    public void SaveAndExit()
    {
        if (_gameEngine == null) return;

        _uiTimer.Stop();
        _gameEngine.UpdateTotalTime();

        var state = _gameEngine.GetCurrentState();
        state.LastSessionDate = DateTime.Now;

        _sessionManager.UpdateGameState(state);
        _sessionManager.SaveSession();
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        SaveAndExit();
    }
}
