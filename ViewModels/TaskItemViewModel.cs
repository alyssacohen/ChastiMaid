using System.Windows.Input;
using ChastiMaid.Models;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class TaskItemViewModel : ViewModelBase
{
    public int ChoreId { get; }
    public string Name { get; }
    public TimeSpan EstimatedDuration { get; }
    public string EstimatedDurationLabel { get; }
    public ICommand StartCommand { get; }

    public TaskItemViewModel(Chore chore, Action<TaskItemViewModel> onStart)
    {
        ChoreId = chore.Id;
        Name = chore.Name;
        EstimatedDuration = TimeSpan.Zero;
        EstimatedDurationLabel = "";
        StartCommand = ReactiveCommand.Create(() => onStart(this));
    }
}
