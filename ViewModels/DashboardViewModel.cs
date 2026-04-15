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

    public string DebtDisplay { get; }
    public ObservableCollection<ChoreItemViewModel> Chores { get; } = [];

    public ICommand EndSessionCommand { get; }

    public DashboardViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var state = saveRepo.GetGameState();
        DebtDisplay = $"Debt: {state?.CurrentDebt:F0} credits";

        foreach (var chore in saveRepo.GetChores())
            Chores.Add(new ChoreItemViewModel(chore.Name, chore.Value));

        EndSessionCommand = ReactiveCommand.Create(EndSession);
    }

    private void EndSession()
    {
        var sessions = _saveRepo.GetSessions();
        var current = sessions.LastOrDefault(s => s.EndedAt is null);
        if (current is not null)
        {
            current.EndedAt = DateTime.UtcNow;
            current.DebtAtEnd = _saveRepo.GetGameState()?.CurrentDebt ?? 0;
            _saveRepo.UpdateSession(current);
        }

        var state = _saveRepo.GetGameState();
        if (state is not null)
        {
            state.LastSessionAt = DateTime.UtcNow;
            _saveRepo.SaveGameState(state);
        }

        _mainVm.NavigateTo(new WelcomeViewModel(_mainVm));
    }
}
