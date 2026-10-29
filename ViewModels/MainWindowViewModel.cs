using System.Threading.Tasks;
using ChastiMaid.Localization;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using LiteDB;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>DB of the currently open save slot. Disposed when switching slots or returning to the menu.</summary>
    private LiteDatabase? _activeSaveDb;

    /// <summary>
    ///     Cached Settings screen, reused across visits so navigating there doesn't re-run its whole
    ///     constructor (DB reads + per-item ViewModel/command allocation) every time. Replaced whenever
    ///     Settings rebuilds itself from the repository (import, discard changes) via <see cref="ReplaceSettingsCache" />.
    /// </summary>
    private SettingsViewModel? _cachedSettingsVm;

    [Reactive] private ViewModelBase _currentPage = null!;

    public MainWindowViewModel(DatabaseService databaseService, ProfileRepository profileRepository,
        SaveSlotManager saveSlotManager, AutoSaveService autoSaveService, WebcamPreviewService webcam,
        SherpaOnnxTtsService tts, SherpaOnnxSttService stt, ILocalizer localizer)
    {
        DatabaseService = databaseService;
        ProfileRepository = profileRepository;
        SaveSlotManager = saveSlotManager;
        AutoSaveService = autoSaveService;
        Webcam = webcam;
        Webcam.PreferredCameraChanged += OnPreferredCameraChanged;
        Tts = tts;
        Stt = stt;
        Localizer = localizer;
        CurrentPage = new LoadingViewModel();
        _ = InitializeAsync();
    }

    public DatabaseService DatabaseService { get; }
    public ProfileRepository ProfileRepository { get; }
    public SaveSlotManager SaveSlotManager { get; }
    public AutoSaveService AutoSaveService { get; }
    public WebcamPreviewService Webcam { get; }
    public SherpaOnnxTtsService Tts { get; }
    public SherpaOnnxSttService Stt { get; }
    public ILocalizer Localizer { get; }

    /// <summary>
    ///     Opens a save slot and tracks its DB for automatic disposal.
    ///     If a DB was already open, it is closed first.
    /// </summary>
    public SaveRepository OpenSaveSlot(string slotName)
    {
        _activeSaveDb?.Dispose();
        _activeSaveDb = DatabaseService.OpenSave(slotName);
        var repo = new SaveRepository(_activeSaveDb);
        repo.ResolveOrphanedWritingLinesAttempts();
        return repo;
    }

    /// <summary>The screen an opened save resumes on: the completion screen once the game is over, otherwise the outfit.</summary>
    public ViewModelBase ResumePage(SaveRepository saveRepo) =>
        saveRepo.GetGameState()?.IsCompleted == true
            ? new GameCompleteViewModel(this, saveRepo)
            : new StartSessionViewModel(this, saveRepo);

    /// <summary>Closes the active save DB (if any).</summary>
    public void CloseActiveSave()
    {
        _activeSaveDb?.Dispose();
        _activeSaveDb = null;
    }

    /// <summary>
    ///     The camera is a device setting, so a pick made in any live selector goes to the profile — not the
    ///     save, whose settings are a new-game snapshot — and into the cached Settings screen.
    /// </summary>
    private void OnPreferredCameraChanged(int index)
    {
        var settings = ProfileRepository.GetGameSettings();
        settings.SelectedCameraIndex = index;
        ProfileRepository.SaveGameSettings(settings);
        _cachedSettingsVm?.SyncSelectedCamera(index);
    }

    public void NavigateTo(ViewModelBase page)
    {
        CurrentPage = page;
    }

    /// <summary>
    ///     Warms the Settings cache and builds the Welcome screen off the UI thread — both do encrypted
    ///     LiteDB reads (profile data, save slot listing) that would otherwise stall the first tab open
    ///     or the Welcome screen itself. Shown behind <see cref="LoadingViewModel" /> instead.
    /// </summary>
    private async Task InitializeAsync()
    {
        var welcomeVm = await Task.Run(() =>
        {
            OpenSettings(() => new WelcomeViewModel(this));
            return new WelcomeViewModel(this);
        });

        CurrentPage = welcomeVm;
    }

    /// <summary>Returns the cached Settings screen, creating it on first use, and points it back at <paramref name="returnTo" />.</summary>
    public SettingsViewModel OpenSettings(Func<ViewModelBase> returnTo)
    {
        if (_cachedSettingsVm is null)
        {
            _cachedSettingsVm = new SettingsViewModel(this, returnTo);
        }
        else
        {
            _cachedSettingsVm.SetReturnTarget(returnTo);
        }

        return _cachedSettingsVm;
    }

    /// <summary>Drops the cached Settings screen so the next <see cref="OpenSettings" /> rebuilds it from the repository.</summary>
    public void InvalidateSettingsCache()
    {
        _cachedSettingsVm = null;
    }

    /// <summary>Swaps the cached Settings instance, used when Settings rebuilds itself from the repository.</summary>
    public void ReplaceSettingsCache(SettingsViewModel vm)
    {
        _cachedSettingsVm = vm;
    }
}
