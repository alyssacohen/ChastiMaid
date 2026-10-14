using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

/// <summary>Shared scaffolding for a punishment "execution" screen (Cornertime, Writing Lines): return-to-dashboard
/// navigation and the resolve-on-success/fine-on-failure outcome shared by every punishment type.</summary>
public abstract class PunishmentExecutionViewModelBase : ViewModelBase
{
    protected readonly MainWindowViewModel MainVm;
    protected readonly ActivePunishment Punishment;
    protected readonly SaveRepository SaveRepo;

    protected ILocalizer L => MainVm.Localizer;

    protected PunishmentExecutionViewModelBase(MainWindowViewModel mainVm, SaveRepository saveRepo, ActivePunishment punishment)
    {
        MainVm = mainVm;
        SaveRepo = saveRepo;
        Punishment = punishment;
        DoneCommand = ReactiveCommand.Create(() => MainVm.NavigateTo(new DashboardViewModel(MainVm, SaveRepo)));
    }

    public ICommand DoneCommand { get; }

    /// <summary>Resolves the punishment on success or applies a fine on failure, returning the message to show.</summary>
    protected string Conclude(bool success, string successMessage, string failureMessage)
    {
        if (success)
        {
            SaveRepo.ResolvePunishment(Punishment.Id);
            return successMessage;
        }

        SaveRepo.ApplyFine(Punishment.Severity);
        return failureMessage;
    }
}
