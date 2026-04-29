using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

    private const double ReferenceHeight = 1080.0;

    private void RegisterScaledResources(MainWindow window)
    {
        var screen = window.Screens.Primary;
        var height = screen?.Bounds.Height ?? ReferenceHeight;
        var scale = height / ReferenceHeight;

        Resources["FontSizeBody"]   = Math.Round(24 * scale);
        Resources["FontSizeSmall"]  = Math.Round(14 * scale);
        Resources["FontSizeButton"] = Math.Round(20 * scale);
        Resources["FontSizeH1"]     = Math.Round(48 * scale);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            RegisterScaledResources(mainWindow);

            _databaseService = new DatabaseService();
            var profileDb = _databaseService.OpenProfile();
            var profileRepo = new ProfileRepository(profileDb);
            DataSeeder.SeedIfEmpty(profileRepo);
            var slotManager = new SaveSlotManager(_databaseService);
            _autoSaveService = new AutoSaveService();
            _webcamService = new WebcamPreviewService();

            mainWindow.DataContext = new MainWindowViewModel(_databaseService, profileRepo, slotManager, _autoSaveService, _webcamService);
            desktop.MainWindow = mainWindow;

            desktop.Exit += (_, _) =>
            {
                _webcamService.Dispose();
                _autoSaveService.Dispose();
                _databaseService.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
