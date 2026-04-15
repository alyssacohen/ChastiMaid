using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class SafeLockViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;

    public ICommand ContinueCommand { get; }

    public SafeLockViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        ContinueCommand = ReactiveCommand.Create(() =>
        {
            var state = saveRepo.GetGameState();
            saveRepo.AddSession(new Session
            {
                StartedAt = DateTime.UtcNow,
                DebtAtStart = state?.CurrentDebt ?? 0,
            });
            _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo));
        });
    }
}
