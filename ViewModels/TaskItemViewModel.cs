using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class TaskItemViewModel : ViewModelBase
{
    [Reactive] private bool _isExpanded;

    public TaskItemViewModel(Chore chore, Action<TaskItemViewModel> onStart)
    {
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

    // Green: not yet due. Amber: overdue by up to 1x the suggested frequency. Red: badly overdue or never done.
    // Occasional tasks have no cadence to be overdue against, so they always show the neutral/idle brush.
    public string StatusBrushKey
    {
        get
        {
            if (IsOccasional)
            {
                return "BrushStatusIdle";
            }

            if (LastCompletedAt is not { } d)
            {
                return "BrushButtonDanger";
            }

            var daysSince = (DateTime.UtcNow - d).Days;
            if (daysSince <= FrequencyDays)
            {
                return "BrushStatusIdle";
            }

            return daysSince <= FrequencyDays * 2 ? "BrushAmber" : "BrushButtonDanger";
        }
    }
}
