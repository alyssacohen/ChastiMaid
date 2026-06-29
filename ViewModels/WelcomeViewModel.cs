using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ConfirmDialogViewModel? _activeDialog;

    [Reactive] private string _clockOffset = "";

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
                var dialog = new ConfirmDialogViewModel(
                    "No Free Slots",
                    "All 5 save slots are occupied. Please complete or delete an existing game before starting a new one.",
                    "OK",
                    null);
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
            {
                desktop.Shutdown();
            }
        });

        SettingsCommand = ReactiveCommand.Create(() =>
            _mainVm.NavigateTo(new SettingsViewModel(_mainVm, () => new WelcomeViewModel(_mainVm))));

        TimeTravelCommand = ReactiveCommand.Create<string>(days =>
        {
            Advance(TimeSpan.FromDays(int.Parse(days)));
            ClockOffset = $"⏩ +{UtcNow() - DateTime.UtcNow:d\\.hh\\:mm} ahead";
        });
    }

    public bool HasActiveGame { get; }

    public ICommand ContinueCommand { get; }
    public ICommand LoadGameCommand { get; }
    public ICommand NewGameCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand TimeTravelCommand { get; }

    public bool IsTestMode => Program.TestMode;
}
