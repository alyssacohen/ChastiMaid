using ChastiMaid.Services;
using LiteDB;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public DatabaseService DatabaseService { get; }
    public ProfileRepository ProfileRepository { get; }
    public SaveSlotManager SaveSlotManager { get; }
    public AutoSaveService AutoSaveService { get; }
    public WebcamPreviewService Webcam { get; }

    /// <summary>DB del save slot attualmente aperto. Viene disposto quando si cambia slot o si torna al menu.</summary>
    private LiteDatabase? _activeSaveDb;

    private ViewModelBase _currentPage = null!;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

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
    /// Apre un save slot e ne traccia il DB per il dispose automatico.
    /// Se c'era un DB aperto, viene chiuso prima.
    /// </summary>
    public SaveRepository OpenSaveSlot(string slotName)
    {
        _activeSaveDb?.Dispose();
        _activeSaveDb = DatabaseService.OpenSave(slotName);
        return new SaveRepository(_activeSaveDb);
    }

    /// <summary>Chiude il save DB attivo (se presente).</summary>
    public void CloseActiveSave()
    {
        _activeSaveDb?.Dispose();
        _activeSaveDb = null;
    }

    public void NavigateTo(ViewModelBase page) => CurrentPage = page;
}
