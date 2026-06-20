using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.ViewModels;
using ChastiMaid.Views;

namespace ChastiMaid;

public partial class App : Application
{
    private DatabaseService? _databaseService;
    private AutoSaveService? _autoSaveService;
    private WebcamPreviewService? _webcamService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();

            _databaseService = new DatabaseService();
            var profileDb = _databaseService.OpenProfile();
            var profileRepo = new ProfileRepository(profileDb);
            DataSeeder.SeedIfEmpty(profileRepo);
            var slotManager = new SaveSlotManager(_databaseService);
            _autoSaveService = new AutoSaveService();
            _webcamService = new WebcamPreviewService();

            mainWindow.DataContext = new MainWindowViewModel(_databaseService, profileRepo, slotManager, _autoSaveService, _webcamService);
            desktop.MainWindow = mainWindow;
            ApplyWindowGeometry(mainWindow);

            desktop.Exit += (_, _) =>
            {
                _webcamService.Dispose();
                _autoSaveService.Dispose();
                (mainWindow.DataContext as MainWindowViewModel)?.CloseActiveSave();
                _databaseService.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Applies startup window size based on command-line flags.
    ///
    /// --portrait   800×1200  (Surface Go portrait,  150% DPI logical pixels)
    /// --landscape  1200×800  (Surface Go landscape, 150% DPI logical pixels)
    /// No flag      normal startup (maximized or last saved state)
    /// </summary>
    private static void ApplyWindowGeometry(Window window)
    {
        if (Program.ForcePortrait)
        {
            window.Width  = 800;
            window.Height = 1200;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.WindowState = WindowState.Normal;
        }
        else if (Program.ForceLandscape)
        {
            window.Width  = 1200;
            window.Height = 800;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.WindowState = WindowState.Normal;
        }
    }
}
