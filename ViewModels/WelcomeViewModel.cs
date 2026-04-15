using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public record SaveSlotItemViewModel(string SlotName, string DisplayDate, string DebtDisplay);

public class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    public ObservableCollection<SaveSlotItemViewModel> SaveSlots { get; } = [];

    private SaveSlotItemViewModel? _selectedSlot;
    public SaveSlotItemViewModel? SelectedSlot
    {
        get => _selectedSlot;
        set => this.RaiseAndSetIfChanged(ref _selectedSlot, value);
    }

    public ICommand NewGameCommand { get; }
    public ICommand ContinueCommand { get; }
    public ICommand ExitCommand { get; }

    public WelcomeViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;

        foreach (var slot in mainVm.SaveSlotManager.ListSlots())
            SaveSlots.Add(new SaveSlotItemViewModel(
                slot.SlotName,
                slot.LastSessionAt.ToLocalTime().ToString("dd/MM/yyyy"),
                $"{slot.CurrentDebt:F0} credits"));

        _selectedSlot = SaveSlots.FirstOrDefault();

        NewGameCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new NewGameViewModel(_mainVm)));

        ContinueCommand = ReactiveCommand.Create(
            () =>
            {
                if (SelectedSlot is null) return;
                var db = mainVm.DatabaseService.OpenSave(SelectedSlot.SlotName);
                var saveRepo = new SaveRepository(db);
                _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, saveRepo));
            },
            canExecute: this.WhenAnyValue(x => x.SelectedSlot, (SaveSlotItemViewModel? s) => s is not null));

        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        });
    }
}
