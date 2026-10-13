using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChastiMaid.Localization;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.ViewModels;
using ChastiMaid.Views;

namespace ChastiMaid;

public class App : Application
{
    private AutoSaveService? _autoSaveService;
    private DatabaseService? _databaseService;
    private WebcamPreviewService? _webcamService;
    private SherpaOnnxTtsService? _ttsService;
    private SherpaOnnxSttService? _sttService;

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
            var settings = profileRepo.GetGameSettings();
            ApplyLanguage(settings.Language);
            if (settings.Language == "system")
            {
                // First run (or a profile from before the selector dropped "System"): pin the resolved language.
                settings.Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                settings.PlayerName = Localization.Resources.Get("DefaultPlayerName");
                profileRepo.SaveGameSettings(settings);
            }

            DataSeeder.SeedIfEmpty(profileRepo, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            var slotManager = new SaveSlotManager(_databaseService);
            _autoSaveService = new AutoSaveService();
            _webcamService = new WebcamPreviewService
            {
                PreferredCameraIndex = profileRepo.GetGameSettings().SelectedCameraIndex
            };
            _ttsService = new SherpaOnnxTtsService();
            _sttService = new SherpaOnnxSttService();

            // Loading the Whisper model parses ~200 MB of ONNX weights and can take several seconds; warming
            // it up now means later synchronous IsModelReady checks (hardware warnings, mic detection) rarely
            // have to pay that cost on the UI thread.
            _ = Task.Run(() => _sttService.IsModelReady);

            mainWindow.DataContext = new MainWindowViewModel(_databaseService, profileRepo, slotManager,
                _autoSaveService, _webcamService, _ttsService, _sttService, Localizer.Current);
            desktop.MainWindow = mainWindow;
            ApplyWindowGeometry(mainWindow);

            desktop.Exit += (_, _) =>
            {
                _webcamService.Dispose();
                _ttsService.Dispose();
                _sttService.Dispose();
                _autoSaveService.Dispose();
                (mainWindow.DataContext as MainWindowViewModel)?.CloseActiveSave();
                _databaseService.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static readonly string[] SupportedLanguages = ["en", "it"];

    /// <summary>
    ///     Resolves the persisted language preference ("system", "en", or "it") to a UI culture,
    ///     sets <see cref="CultureInfo.CurrentUICulture" /> and loads that language's translation catalog.
    ///     Must run before any view is constructed.
    /// </summary>
    internal static void ApplyLanguage(string language)
    {
        var code = language == "system"
            ? SupportedLanguages.FirstOrDefault(l => CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == l) ?? "en"
            : language;

        code = SupportedLanguages.Contains(code) ? code : "en";
        CultureInfo.CurrentUICulture = new CultureInfo(code);
        Localizer.Current.SetLanguage(code);
    }

    /// <summary>
    ///     Applies startup window size based on command-line flags.
    ///     --portrait   800×1200  (Surface Go portrait,  150% DPI logical pixels)
    ///     --landscape  1200×800  (Surface Go landscape, 150% DPI logical pixels)
    ///     No flag      normal startup (maximized or last saved state)
    /// </summary>
    private static void ApplyWindowGeometry(Window window)
    {
        if (Program.ForcePortrait)
        {
            window.Width = 800;
            window.Height = 1200;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.WindowState = WindowState.Normal;
        }
        else if (Program.ForceLandscape)
        {
            window.Width = 1200;
            window.Height = 800;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.WindowState = WindowState.Normal;
        }
    }
}
