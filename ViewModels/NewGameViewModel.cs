using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public record DifficultyOptionViewModel(
    Difficulty Difficulty,
    string Label,
    string DebtRange,
    string InterestRate);

public class NewGameViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    public IReadOnlyList<DifficultyOptionViewModel> Options { get; } =
    [
        BuildOption(Difficulty.Easy,   "Easy"),
        BuildOption(Difficulty.Medium, "Medium"),
        BuildOption(Difficulty.Hard,   "Hard"),
    ];

    public ICommand SelectCommand { get; }
    public ICommand BackCommand { get; }

    public NewGameViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;

        SelectCommand = ReactiveCommand.Create<Difficulty>(StartGame);
        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new WelcomeViewModel(_mainVm)));
    }

    private void StartGame(Difficulty difficulty)
    {
        var debt = DifficultySettings.RollDebt(difficulty);
        var slotName = $"save_{DateTime.Now:yyyyMMdd_HHmm}";

        var saveRepo = SaveRepository.CreateFromProfile(
            _mainVm.ProfileRepository, _mainVm.DatabaseService, slotName, debt);

        _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, saveRepo));
    }

    private static DifficultyOptionViewModel BuildOption(Difficulty d, string label)
    {
        var cfg = DifficultySettings.Get(d);
        return new DifficultyOptionViewModel(
            d, label,
            $"{cfg.MinDebt:F0} – {cfg.MaxDebt:F0} credits",
            $"{cfg.DailyInterestRate * 100:F0}% / day");
    }
}
