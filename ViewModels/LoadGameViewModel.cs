using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Services;
using ChastiMaid.Services.Contracts;
using FluentIcons.Common;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class SlotCardViewModel : ViewModelBase
{
    // Empty slot
    public SlotCardViewModel(int number)
    {
        SlotNumber = number;
        IsEmpty = true;
        SlotName = "";
        SavedAt = "";
        CurrentDebt = 0;
        BalanceLabel = "";
        SafeStatus = "";
        CompletedLabel = "";
        LoadCommand = ReactiveCommand.Create(() => { });
        InfoCommand = ReactiveCommand.Create(() => { });
        DeleteCommand = ReactiveCommand.Create(() => { });
    }

    // Filled slot
    public SlotCardViewModel(int number, SaveSlotInfo slot, LoadGameViewModel parent, ILocalizer l)
    {
        SlotNumber = number;
        IsEmpty = false;
        SlotName = slot.SlotName;
        SavedAt = slot.LastSessionAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        CurrentDebt = slot.CurrentDebt;
        // A negative debt is accumulated credit.
        BalanceLabel = slot.CurrentDebt < 0
            ? l.T("Credit: £{0:F2}", -slot.CurrentDebt)
            : l.T("Debt: £{0:F2}", slot.CurrentDebt);
        IsSafeLocked = slot.IsSafeLocked;
        // translators: state of the safe holding the key (feminine in Italian: "la cassaforte")
        SafeStatus = slot.IsSafeLocked ? l.P("safe", "Locked") : l.P("safe", "Open");
        IsCompleted = slot.IsCompleted;
        // translators: a finished game (feminine in Italian: "la partita")
        CompletedLabel = slot.IsCompleted ? l.P("game", "Completed") : "";

        var canLoad = Observable.Return(!slot.IsCompleted);
        LoadCommand = ReactiveCommand.CreateFromTask(() => parent.LoadSlot(slot.SlotName), canLoad);
        InfoCommand = ReactiveCommand.CreateFromTask(() => parent.ShowContract(slot.SlotName));
        DeleteCommand = ReactiveCommand.CreateFromTask(() => parent.ConfirmDeleteSlot(this));
    }

    public int SlotNumber { get; }
    public bool IsEmpty { get; }
    public string SlotName { get; }
    public string SavedAt { get; }
    public decimal CurrentDebt { get; }
    public string BalanceLabel { get; }
    public string SafeStatus { get; }
    public bool IsSafeLocked { get; }
    public Symbol SafeIcon => IsSafeLocked ? Symbol.LockClosed : Symbol.LockOpen;
    public bool IsCompleted { get; }
    public string CompletedLabel { get; }

    public ICommand LoadCommand { get; }
    public ICommand InfoCommand { get; }
    public ICommand DeleteCommand { get; }
}

public partial class LoadGameViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private IReadOnlyList<ContractClause>? _contractClauses;

    public LoadGameViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;

        RefreshSlots();

        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new WelcomeViewModel(_mainVm)));
        CloseContractCommand = ReactiveCommand.Create(() => { ContractClauses = null; });
    }

    public ObservableCollection<SlotCardViewModel> Slots { get; } = [];

    /// <summary>The clauses of the save being viewed read-only; null while the contract overlay is closed.</summary>
    public IReadOnlyList<ContractClause>? ContractClauses
    {
        get => _contractClauses;
        set => this.RaiseAndSetIfChanged(ref _contractClauses, value);
    }

    public ICommand BackCommand { get; }
    public ICommand CloseContractCommand { get; }

    public void RefreshSlots()
    {
        Slots.Clear();

        var saves = _mainVm.SaveSlotManager.ListSlots()
            .OrderByDescending(s => s.LastSessionAt)
            .ToList();

        for (var i = 0; i < 5; i++)
        {
            Slots.Add(i < saves.Count
                ? new SlotCardViewModel(i + 1, saves[i], this, _mainVm.Localizer)
                : new SlotCardViewModel(i + 1));
        }
    }

    public async Task LoadSlot(string slotName)
    {
        try
        {
            var saveRepo = _mainVm.OpenSaveSlot(slotName);
            _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, saveRepo));
        }
        catch (Exception ex)
        {
            await ShowLoadErrorAsync(slotName, ex);
        }
    }

    public async Task ShowContract(string slotName)
    {
        try
        {
            ContractClauses = ContractClauseBuilder.Build(_mainVm.SaveSlotManager.ReadContract(slotName));
        }
        catch (Exception ex)
        {
            await ShowLoadErrorAsync(slotName, ex);
        }
    }

    private Task<bool> ShowLoadErrorAsync(string slotName, Exception ex)
    {
        var l = _mainVm.Localizer;
        return ShowConfirmAsync(new ConfirmDialogViewModel(
            l.T("Load Error"),
            // translators: {0} is the save name, {1} the technical error message
            l.T("Could not load \"{0}\". The save file may be corrupted.\n\n{1}", slotName, ex.Message),
            l.T("OK"),
            null));
    }

    public async Task ConfirmDeleteSlot(SlotCardViewModel slot)
    {
        var l = _mainVm.Localizer;
        var message = slot.IsSafeLocked
            ? l.T("Delete \"{0}\"?\n\nWARNING: The safe is locked. Deleting this save will permanently destroy the encrypted combination photo. The combination will be lost forever.", slot.SlotName)
            : l.T("Delete \"{0}\"? This game will be marked as failed.", slot.SlotName);

        var dialog = new ConfirmDialogViewModel(
            l.T("Delete Save"),
            message,
            l.T("Delete"),
            l.T("Cancel"));

        var confirmed = await ShowConfirmAsync(dialog);

        if (!confirmed)
        {
            return;
        }

        _mainVm.SaveSlotManager.DeleteSlot(slot.SlotName);

        var stats = _mainVm.ProfileRepository.GetStats();
        stats.GamesFailed++;
        _mainVm.ProfileRepository.SaveStats(stats);

        RefreshSlots();
    }
}
