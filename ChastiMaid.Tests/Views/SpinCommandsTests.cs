using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChastiMaid.Tests.Views;

/// <summary>
///     The routing <c>Views/Behaviors/SpinCommands</c> relies on: a class handler on the spinner runs first, and once
///     it marks the spin handled, an instance handler added with <c>+=</c> (how NumericUpDown listens) never runs.
/// </summary>
public class SpinCommandsTests
{
    [Fact]
    public void AClassHandler_RunsBeforeAndSuppressesTheSpinnersInstanceHandler()
    {
        var spinner = new ButtonSpinner();
        var calls = new List<string>();
        spinner.Spin += (_, _) => calls.Add("instance");
        using var classHandler = Spinner.SpinEvent.AddClassHandler<ButtonSpinner>((sender, e) =>
        {
            if (ReferenceEquals(sender, spinner))
            {
                calls.Add("class");
                e.Handled = true;
            }
        });

        spinner.RaiseEvent(new SpinEventArgs(Spinner.SpinEvent, SpinDirection.Increase));

        Assert.Equal(["class"], calls);
    }
}
