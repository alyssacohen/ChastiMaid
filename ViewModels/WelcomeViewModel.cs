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
        // A completed game still counts until its slot is freed: Continue reopens its completion screen.
        HasActiveGame = latest is not null;

        ContinueCommand = ReactiveCommand.Create(
            () =>
            {
                var saveRepo = mainVm.OpenSaveSlot(latest!.SlotName);
                _mainVm.NavigateTo(_mainVm.ResumePage(saveRepo));
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

            // No name yet (first game, or never set in Settings): the player introduces themselves first.
            var settings = _mainVm.ProfileRepository.GetGameSettings();
            if (string.IsNullOrWhiteSpace(settings.PlayerName))
            {
                var intro = new PlayerIntroDialogViewModel(_mainVm.Localizer);
                IntroDialog = intro;
                var introduced = await intro.Result;
                IntroDialog = null;
                if (!introduced)
                {
                    return;
                }

                settings.PlayerName = intro.Name.Trim();
                settings.Appellatives = intro.Appellatives.Trim();
                _mainVm.ProfileRepository.SaveGameSettings(settings);
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

    private PlayerIntroDialogViewModel? _introDialog;

    /// <summary>The "introduce yourself" dialog while it is open.</summary>
    public PlayerIntroDialogViewModel? IntroDialog
    {
        get => _introDialog;
        set => this.RaiseAndSetIfChanged(ref _introDialog, value);
    }

    public ICommand ContinueCommand { get; }
    public ICommand LoadGameCommand { get; }
    public ICommand NewGameCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }
}
