using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class TaskItemViewModel : ViewModelBase
{
    private readonly Chore _chore;
    private readonly DateTime _gameStartedAt;
    [Reactive] private bool _isExpanded;

    private ILocalizer L { get; }

    public TaskItemViewModel(Chore chore, DateTime gameStartedAt, Action<TaskItemViewModel> onStart, ILocalizer l)
    {
        L = l;
        _chore = chore;
        _gameStartedAt = gameStartedAt;
        ChoreId = chore.Id;
        Name = chore.Name;
        FrequencyDays = chore.FrequencyDays;
        LastCompletedAt = chore.LastCompletedAt;
        IsOccasional = chore.IsOccasional;
        StartCommand = ReactiveCommand.Create(() => onStart(this));
        ToggleExpandedCommand = ReactiveCommand.Create(() => IsExpanded = !IsExpanded);
    }

    public int ChoreId { get; }
    public string Name { get; }
    public int FrequencyDays { get; }
    public DateTime? LastCompletedAt { get; }
    public bool IsOccasional { get; }
    public ICommand StartCommand { get; }
    public ICommand ToggleExpandedCommand { get; }

    public string LastCompletedLabel => LastCompletedAt is { } d
        ? L.N("Last done: {0} day ago", "Last done: {0} days ago", (DateTime.UtcNow - d).Days, (DateTime.UtcNow - d).Days)
        : L.T("Never done");

    public string FrequencyLabel => IsOccasional
        ? L.T("Occasional task")
        : L.N("Suggested every {0} day", "Suggested every {0} days", FrequencyDays, FrequencyDays);

    // Green: not yet due. Amber: overdue by up to 1x the suggested frequency. Red: badly overdue. Same measure the
    // variable-severity mood uses, so a chore never done counts from the start of the game rather than showing red
    // on day one. Occasional tasks have no cadence to be overdue against, so they always show the neutral/idle brush.
    public string StatusBrushKey
    {
        get
        {
            if (IsOccasional)
            {
                return "BrushStatusIdle";
            }

            return SeverityBalance.ChoreLateness(_chore, _gameStartedAt, DateTime.UtcNow) switch
            {
                <= 0 => "BrushStatusIdle",
                < 1 => "BrushAmber",
                _ => "BrushButtonDanger",
            };
        }
    }
}
