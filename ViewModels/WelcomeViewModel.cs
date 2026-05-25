using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using ChastiMaid.Services;
using ReactiveUI;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    public bool HasActiveGame { get; }

    private ConfirmDialogViewModel? _activeDialog;
    public ConfirmDialogViewModel? ActiveDialog
    {
        get => _activeDialog;
        private set => this.RaiseAndSetIfChanged(ref _activeDialog, value);
    }

    public ICommand ContinueCommand { get; }
    public ICommand LoadGameCommand { get; }
    public ICommand NewGameCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand TimeTravelCommand { get; }

    public bool IsTestMode => Program.TestMode;

    private string _clockOffset = "";
    public string ClockOffset { get => _clockOffset; set => this.RaiseAndSetIfChanged(ref _clockOffset, value); }

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
            canExecute: this.WhenAnyValue(x => x.HasActiveGame));

        LoadGameCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new LoadGameViewModel(_mainVm)));

        NewGameCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (slots.Count >= 5)
            {
                var dialog = new ConfirmDialogViewModel(
                    title: "No Free Slots",
                    message: "All 5 save slots are occupied. Please complete or delete an existing game before starting a new one.",
                    confirmLabel: "OK",
                    cancelLabel: null);
                ActiveDialog = dialog;
                await dialog.Result;
                ActiveDialog = null;
                return;
            }
            _mainVm.NavigateTo(new NewGameViewModel(_mainVm));
        });

        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        });

        SettingsCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new SettingsViewModel(_mainVm, new WelcomeViewModel(_mainVm))));

        TimeTravelCommand = ReactiveCommand.Create<string>(days =>
        {
            GameClock.Advance(TimeSpan.FromDays(int.Parse(days)));
            ClockOffset = $"⏩ +{GameClock.UtcNow() - DateTime.UtcNow:d\\.hh\\:mm} ahead";
        });
    }
}
