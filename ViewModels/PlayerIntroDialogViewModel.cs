using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Asked before the first new game when the profile has no name yet: the player's name (required) and,
///     optionally, the appellatives used in summons. Await <see cref="Result" />: true once both are entered.
/// </summary>
public class PlayerIntroDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly ILocalizer _l;
    private string _name = "";
    private string _appellatives = "";

    public PlayerIntroDialogViewModel(ILocalizer l)
    {
        _l = l;
        ConfirmCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(true),
            this.WhenAnyValue(x => x.Name, n => !string.IsNullOrWhiteSpace(n)));
        CancelCommand = ReactiveCommand.Create(() => _tcs.TrySetResult(false));
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Appellatives
    {
        get => _appellatives;
        set
        {
            this.RaiseAndSetIfChanged(ref _appellatives, value);
            this.RaisePropertyChanged(nameof(AppellativesWarning));
        }
    }

    public string? AppellativesWarning => GameSettings.AppellativesWarning(Appellatives, _l);

    public Task<bool> Result => _tcs.Task;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
}
