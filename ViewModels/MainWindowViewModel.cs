using ChastiMaid.Repositories;
using ChastiMaid.Services;
using LiteDB;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public DatabaseService DatabaseService { get; }
    public ProfileRepository ProfileRepository { get; }
    public SaveSlotManager SaveSlotManager { get; }
    public AutoSaveService AutoSaveService { get; }
    public WebcamPreviewService Webcam { get; }

    /// <summary>DB of the currently open save slot. Disposed when switching slots or returning to the menu.</summary>
    private LiteDatabase? _activeSaveDb;

    [Reactive] private ViewModelBase _currentPage = null!;

    public MainWindowViewModel(DatabaseService databaseService, ProfileRepository profileRepository, SaveSlotManager saveSlotManager, AutoSaveService autoSaveService, WebcamPreviewService webcam)
    {
        DatabaseService = databaseService;
        ProfileRepository = profileRepository;
        SaveSlotManager = saveSlotManager;
        AutoSaveService = autoSaveService;
        Webcam = webcam;
        CurrentPage = new WelcomeViewModel(this);
    }

    /// <summary>
    /// Opens a save slot and tracks its DB for automatic disposal.
    /// If a DB was already open, it is closed first.
    ///</summary>
    public SaveRepository OpenSaveSlot(string slotName)
    {
        _activeSaveDb?.Dispose();
        _activeSaveDb = DatabaseService.OpenSave(slotName);
        return new SaveRepository(_activeSaveDb);
    }

    /// <summary>Closes the active save DB (if any).</summary>
    public void CloseActiveSave()
    {
        _activeSaveDb?.Dispose();
        _activeSaveDb = null;
    }

    public void NavigateTo(ViewModelBase page) => CurrentPage = page;
}
