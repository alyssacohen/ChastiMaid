using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

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
        DebtDisplay = "";
        SafeStatus = "";
        LoadCommand = ReactiveCommand.Create(() => { });
        DeleteCommand = ReactiveCommand.Create(() => { });
    }

    // Filled slot
    public SlotCardViewModel(int number, SaveSlotInfo slot, LoadGameViewModel parent)
    {
        SlotNumber = number;
        IsEmpty = false;
        SlotName = slot.SlotName;
        SavedAt = slot.LastSessionAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        DebtDisplay = $"${slot.CurrentDebt:F2}";
        IsSafeLocked = slot.IsSafeLocked;
        SafeStatus = slot.IsSafeLocked ? "🔒 Locked" : "🔓 Open";
        IsCompleted = slot.IsCompleted;

        var canLoad = Observable.Return(!slot.IsCompleted);
        LoadCommand = ReactiveCommand.CreateFromTask(() => parent.LoadSlot(slot.SlotName), canLoad);
        DeleteCommand = ReactiveCommand.CreateFromTask(() => parent.ConfirmDeleteSlot(this));
    }

    public int SlotNumber { get; }
    public bool IsEmpty { get; }
    public string SlotName { get; }
    public string SavedAt { get; }
    public string DebtDisplay { get; }
    public string SafeStatus { get; }
    public bool IsSafeLocked { get; }
    public bool IsCompleted { get; }
    public string CompletedLabel => IsCompleted ? "✓ Completed" : "";

    public ICommand LoadCommand { get; }
    public ICommand DeleteCommand { get; }
}

public partial class LoadGameViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ConfirmDialogViewModel? _activeDialog;

    public LoadGameViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;

        RefreshSlots();

        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new WelcomeViewModel(_mainVm)));
    }

    public ObservableCollection<SlotCardViewModel> Slots { get; } = [];

    public ICommand BackCommand { get; }

    public void RefreshSlots()
    {
        Slots.Clear();

        var saves = _mainVm.SaveSlotManager.ListSlots()
            .OrderByDescending(s => s.LastSessionAt)
            .ToList();

        for (var i = 0; i < 5; i++)
        {
            Slots.Add(i < saves.Count
                ? new SlotCardViewModel(i + 1, saves[i], this)
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
            var dialog = new ConfirmDialogViewModel(
                "Load Error",
                $"Could not load \"{slotName}\". The save file may be corrupted.\n\n{ex.Message}",
                "OK",
                null);

            ActiveDialog = dialog;
            await dialog.Result;
            ActiveDialog = null;
        }
    }

    public async Task ConfirmDeleteSlot(SlotCardViewModel slot)
    {
        var message = slot.IsSafeLocked
            ? $"Delete \"{slot.SlotName}\"?\n\nWARNING: The safe is locked. Deleting this save will permanently destroy the encrypted combination photo. The combination will be lost forever."
            : $"Delete \"{slot.SlotName}\"? This game will be marked as failed.";

        var dialog = new ConfirmDialogViewModel(
            "Delete Save",
            message,
            "Delete",
            "Cancel");

        ActiveDialog = dialog;
        var confirmed = await dialog.Result;
        ActiveDialog = null;

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
