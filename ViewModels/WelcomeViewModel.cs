using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    public WelcomeViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;

        var slots = mainVm.SaveSlotManager.ListSlots();
        var latest = slots.MaxBy(s => s.LastSessionAt);
        HasActiveGame = latest is not null && !latest.IsCompleted;

        ContinueCommand = ReactiveCommand.Create(
            () =>
            {
                var saveRepo = mainVm.OpenSaveSlot(latest!.SlotName);
                _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, saveRepo));
            },
            this.WhenAnyValue(x => x.HasActiveGame));

        LoadGameCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new LoadGameViewModel(_mainVm)));

        NewGameCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (slots.Count >= 5)
            {
                var l = _mainVm.Localizer;
                var dialog = new ConfirmDialogViewModel(
                    l.T("No Free Slots"),
                    l.T("All 5 save slots are occupied. Please complete or delete an existing game before starting a new one."),
                    l.T("OK"),
                    null);
                await ShowConfirmAsync(dialog);
                return;
            }

            _mainVm.NavigateTo(new NewGameViewModel(_mainVm));
        });

        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });

        SettingsCommand = ReactiveCommand.Create(() =>
            _mainVm.NavigateTo(_mainVm.OpenSettings(() => new WelcomeViewModel(_mainVm))));
    }

    public bool HasActiveGame { get; }

    public ICommand ContinueCommand { get; }
    public ICommand LoadGameCommand { get; }
    public ICommand NewGameCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }
}
