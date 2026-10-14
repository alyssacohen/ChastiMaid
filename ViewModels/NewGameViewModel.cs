using System.Reactive.Linq;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services.Contracts;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>
///     The New Game screen: the current settings written out as a contract. The clauses are derived from
///     <see cref="Contract" />, so loading a file (or coming back from Settings, which rebuilds this screen)
///     needs no refresh code.
/// </summary>
public partial class NewGameViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly GameContractService _contracts;
    private readonly ObservableAsPropertyHelper<IReadOnlyList<ContractClause>> _clauses;

    [Reactive] private GameContract _contract;

    /// <summary>Outcome of the last "load another contract" attempt; null until one happens.</summary>
    [Reactive] private string? _statusMessage;

    public NewGameViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;
        _contracts = new GameContractService(mainVm.ProfileRepository);
        _contract = _contracts.Build();

        _clauses = this.WhenAnyValue(x => x.Contract)
            .Select(new ContractClauseBuilder(mainVm.Localizer).Build)
            .ToProperty(this, x => x.Clauses);

        // Same as the former "Start Game": the debt is rolled from the profile settings at the moment of acceptance.
        AcceptCommand = ReactiveCommand.Create(() =>
        {
            var settings = mainVm.ProfileRepository.GetGameSettings();
            var debt = settings.RollDebt();
            var slotName = $"save_{DateTime.Now:yyyyMMdd_HHmm}";
            var saveRepo = SaveRepository.CreateFromProfile(
                mainVm.ProfileRepository, mainVm.DatabaseService, slotName, debt);
            mainVm.NavigateTo(new StartSessionViewModel(mainVm, saveRepo));
        });

        // Settings returns to a fresh instance of this screen, which reads the edited profile.
        EditCommand = ReactiveCommand.Create(() =>
            mainVm.NavigateTo(mainVm.OpenSettings(() => new NewGameViewModel(mainVm))));

        LoadContractCommand = ReactiveCommand.CreateFromTask(LoadContract);

        BackCommand = ReactiveCommand.Create(() => mainVm.NavigateTo(new WelcomeViewModel(mainVm)));
    }

    /// <summary>The numbered clauses of <see cref="Contract" />, including the fixed variable-severity clause.</summary>
    public IReadOnlyList<ContractClause> Clauses => _clauses.Value;

    public ICommand AcceptCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand LoadContractCommand { get; }
    public ICommand BackCommand { get; }

    private async Task LoadContract()
    {
        var outcome = await new ContractImportFlow(_contracts, _mainVm.Localizer).RunAsync(ShowConfirmAsync);

        switch (outcome.Kind)
        {
            case ContractImportOutcomeKind.Failed:
                StatusMessage = outcome.Message;
                break;
            case ContractImportOutcomeKind.Applied:
                // The cached Settings screen still holds the old profile values.
                _mainVm.InvalidateSettingsCache();
                Contract = _contracts.Build();
                StatusMessage = outcome.Message;
                break;
        }
    }
}
