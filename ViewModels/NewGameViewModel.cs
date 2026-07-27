using System.Windows.Input;
using ChastiMaid.Repositories;
using ChastiMaid.Views.Converters;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class NewGameViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    public NewGameViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;

        var settings = mainVm.ProfileRepository.GetGameSettings();
        DebtRange = $"{CurrencyConverter.Format(settings.MinDebt)} – {CurrencyConverter.Format(settings.MaxDebt)}";
        InterestRate = $"{settings.DailyInterestRate * 100:F2}% / day";

        StartGameCommand = ReactiveCommand.Create(() =>
        {
            var debt = settings.RollDebt();
            var slotName = $"save_{DateTime.Now:yyyyMMdd_HHmm}";
            var saveRepo = SaveRepository.CreateFromProfile(
                mainVm.ProfileRepository, mainVm.DatabaseService, slotName, debt);
            mainVm.NavigateTo(new StartSessionViewModel(mainVm, saveRepo));
        });

        BackCommand = ReactiveCommand.Create(() => mainVm.NavigateTo(new WelcomeViewModel(mainVm)));

        SettingsCommand = ReactiveCommand.Create(() =>
            mainVm.NavigateTo(new SettingsViewModel(mainVm, () => new NewGameViewModel(mainVm))));
    }

    public string DebtRange { get; }
    public string InterestRate { get; }

    public ICommand StartGameCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand BackCommand { get; }
}
