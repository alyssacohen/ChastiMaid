using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

    /// <summary>
    ///     True when the window width is less than its height (portrait orientation).
    ///     Updated reactively on every window resize — no app restart needed.
    ///     Propagate to child ViewModels that need to adapt their layout.
    /// </summary>
    [Reactive] private bool _isPortrait;

    public MainWindowViewModel(DatabaseService databaseService, ProfileRepository profileRepository,
        SaveSlotManager saveSlotManager, AutoSaveService autoSaveService, WebcamPreviewService webcam)
    {
        DatabaseService = databaseService;
        ProfileRepository = profileRepository;
        SaveSlotManager = saveSlotManager;
        AutoSaveService = autoSaveService;
        Webcam = webcam;
        CurrentPage = new WelcomeViewModel(this);

        if (Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime lifetime
            && lifetime.MainWindow is { } window)
        {
            window.GetObservable(Visual.BoundsProperty)
                .Subscribe(bounds => IsPortrait = bounds.Width < bounds.Height);
        }
    }

    public DatabaseService DatabaseService { get; }
    public ProfileRepository ProfileRepository { get; }
    public SaveSlotManager SaveSlotManager { get; }
    public AutoSaveService AutoSaveService { get; }
    public WebcamPreviewService Webcam { get; }

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

    /// <summary>Closes the active save DB (if any).</summary>
    public void CloseActiveSave()
    {
        _activeSaveDb?.Dispose();
        _activeSaveDb = null;
    }

    public void NavigateTo(ViewModelBase page)
    {
        CurrentPage = page;
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

    /// <summary>Swaps the cached Settings instance, used when Settings rebuilds itself from the repository.</summary>
    public void ReplaceSettingsCache(SettingsViewModel vm)
    {
        _cachedSettingsVm = vm;
    }
}
