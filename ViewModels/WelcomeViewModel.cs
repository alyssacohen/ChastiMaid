using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ChastiMaid.Localization;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ConfirmDialogViewModel? _activeDialog;

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
                    Resources.Get("WelcomeNoFreeSlotsTitle"),
                    Resources.Get("WelcomeNoFreeSlotsMessage"),
                    Resources.Get("Ok"),
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
            _mainVm.NavigateTo(_mainVm.OpenSettings(() => new WelcomeViewModel(_mainVm))));
    }

    public bool HasActiveGame { get; }

    public ICommand ContinueCommand { get; }
    public ICommand LoadGameCommand { get; }
    public ICommand NewGameCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }
}
