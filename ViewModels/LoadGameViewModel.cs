using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class SlotCardViewModel : ViewModelBase
{
    public string SlotName { get; }
    public string SavedAt { get; }
    public string Difficulty { get; }
    public string DebtDisplay { get; }
    public string SafeStatus { get; }
    public bool IsSafeLocked { get; }
    public bool IsCompleted { get; }
    public string CompletedLabel => IsCompleted ? "✓ Completed" : "";
    public double CardOpacity => IsCompleted ? 0.5 : 1.0;

    public ICommand LoadCommand { get; }
    public ICommand DeleteCommand { get; }

    public SlotCardViewModel(SaveSlotInfo slot, LoadGameViewModel parent)
    {
        SlotName = slot.SlotName;
        SavedAt = slot.LastSessionAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Difficulty = slot.Difficulty.ToString();
        DebtDisplay = $"{slot.CurrentDebt:F0} credits";
        IsSafeLocked = slot.IsSafeLocked;
        SafeStatus = slot.IsSafeLocked ? "🔒 Locked" : "🔓 Open";
        IsCompleted = slot.IsCompleted;

        var canLoad = Observable.Return(!slot.IsCompleted);
        LoadCommand = ReactiveCommand.CreateFromTask(() => parent.LoadSlot(slot.SlotName), canLoad);
        DeleteCommand = ReactiveCommand.CreateFromTask(() => parent.ConfirmDeleteSlot(this));
    }
}

public class LoadGameViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    public ObservableCollection<SlotCardViewModel> Slots { get; } = [];

    private ConfirmDialogViewModel? _activeDialog;
    public ConfirmDialogViewModel? ActiveDialog
    {
        get => _activeDialog;
        private set => this.RaiseAndSetIfChanged(ref _activeDialog, value);
    }

    public bool HasSlots => Slots.Count > 0;

    public ICommand BackCommand { get; }

    public LoadGameViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;

        RefreshSlots();

        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new WelcomeViewModel(_mainVm)));
    }

    public void RefreshSlots()
    {
        Slots.Clear();

        foreach (var slot in _mainVm.SaveSlotManager.ListSlots().OrderByDescending(s => s.LastSessionAt))
            Slots.Add(new SlotCardViewModel(slot, this));

        this.RaisePropertyChanged(nameof(HasSlots));
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
                title: "Load Error",
                message: $"Could not load \"{slotName}\". The save file may be corrupted.\n\n{ex.Message}",
                confirmLabel: "OK",
                cancelLabel: null);

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
            title: "Delete Save",
            message: message,
            confirmLabel: "Delete",
            cancelLabel: "Cancel");

        ActiveDialog = dialog;
        var confirmed = await dialog.Result;
        ActiveDialog = null;

        if (!confirmed) return;

        _mainVm.SaveSlotManager.DeleteSlot(slot.SlotName);

        var stats = _mainVm.ProfileRepository.GetStats();
        stats.GamesFailed++;
        _mainVm.ProfileRepository.SaveStats(stats);

        Slots.Remove(slot);
    }
}
