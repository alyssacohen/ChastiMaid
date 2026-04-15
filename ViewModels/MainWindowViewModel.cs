using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public DatabaseService DatabaseService { get; }
    public ProfileRepository ProfileRepository { get; }
    public SaveSlotManager SaveSlotManager { get; }

    private ViewModelBase _currentPage = null!;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public MainWindowViewModel(DatabaseService databaseService, ProfileRepository profileRepository, SaveSlotManager saveSlotManager)
    {
        DatabaseService = databaseService;
        ProfileRepository = profileRepository;
        SaveSlotManager = saveSlotManager;
        CurrentPage = new WelcomeViewModel(this);
    }

    public void NavigateTo(ViewModelBase page) => CurrentPage = page;
}
