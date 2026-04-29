using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public record ChoreItemViewModel(string Name, decimal Value);

public class DashboardViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly AutoSaveService _autoSave;

    public string DebtDisplay { get; }
    public bool CanComplete { get; }
    public ObservableCollection<ChoreItemViewModel> Chores { get; } = [];

    public ICommand EndSessionCommand { get; }
    public ICommand CompleteGameCommand { get; }

    public DashboardViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _autoSave = mainVm.AutoSaveService;

        // Collega il repo attivo e avvia il timer
        _autoSave.Bind(saveRepo);
        var interval = mainVm.ProfileRepository.GetProfile().AutoSaveIntervalMinutes;
        _autoSave.StartTimer(interval);

        var state = saveRepo.GetGameState();
        DebtDisplay = $"Debt: {state?.CurrentDebt:F0} credits";
        CanComplete = state is not null && state.CurrentDebt <= 0;

        foreach (var chore in saveRepo.GetChores())
            Chores.Add(new ChoreItemViewModel(chore.Name, chore.Value));

        EndSessionCommand = ReactiveCommand.Create(EndSession);
        CompleteGameCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new GameCompleteViewModel(_mainVm, _saveRepo)));
    }

    private void EndSession()
    {
        _autoSave.StopTimer();

        var sessions = _saveRepo.GetSessions();
        var current = sessions.LastOrDefault(s => s.EndedAt is null);
        if (current is not null)
        {
            current.EndedAt = DateTime.UtcNow;
            current.DebtAtEnd = _saveRepo.GetGameState()?.CurrentDebt ?? 0;
            _saveRepo.UpdateSession(current);
        }

        // Salvataggio finale della sessione
        _autoSave.TriggerSave();

        _mainVm.NavigateTo(new WelcomeViewModel(_mainVm));
    }
}
