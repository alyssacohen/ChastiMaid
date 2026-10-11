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

    public TaskItemViewModel(Chore chore, DateTime gameStartedAt, Action<TaskItemViewModel> onStart)
    {
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
        ? string.Format(Resources.Get("TaskLastCompletedDaysAgo"), (DateTime.UtcNow - d).Days)
        : Resources.Get("TaskNeverCompleted");

    public string FrequencyLabel => IsOccasional
        ? Resources.Get("TaskOccasionalLabel")
        : string.Format(Resources.Get("TaskSuggestedFrequencyDays"), FrequencyDays);

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
