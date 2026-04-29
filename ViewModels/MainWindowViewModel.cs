using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public DatabaseService DatabaseService { get; }
    public ProfileRepository ProfileRepository { get; }
    public SaveSlotManager SaveSlotManager { get; }
    public AutoSaveService AutoSaveService { get; }
    public WebcamPreviewService Webcam { get; }

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

    public void NavigateTo(ViewModelBase page) => CurrentPage = page;
}
