using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChastiMaid.Views.Behaviors;

/// <summary>
///     Sends a NumericUpDown's spins (spinner buttons, Up/Down keys, mouse wheel) to ViewModel commands instead of the
///     control's own increment. For a field that is empty on purpose (edited items with different values): from an
///     empty value the control starts at its Minimum, which would then be written to every item.
///     <code>&lt;NumericUpDown behaviors:SpinCommands.Increase="{Binding StepUpCommand}"
///     behaviors:SpinCommands.Decrease="{Binding StepDownCommand}" /&gt;</code>
/// </summary>
public sealed class SpinCommands : AvaloniaObject
{
    public static readonly AttachedProperty<ICommand?> IncreaseProperty =
        AvaloniaProperty.RegisterAttached<SpinCommands, NumericUpDown, ICommand?>("Increase");

    public static readonly AttachedProperty<ICommand?> DecreaseProperty =
        AvaloniaProperty.RegisterAttached<SpinCommands, NumericUpDown, ICommand?>("Decrease");

    static SpinCommands()
    {
        // A class handler runs before the NumericUpDown's own handler on its spinner, which skips a handled spin.
        Spinner.SpinEvent.AddClassHandler<ButtonSpinner>(OnSpin);
    }

    private SpinCommands()
    {
    }

    public static ICommand? GetIncrease(NumericUpDown field) => field.GetValue(IncreaseProperty);

    public static void SetIncrease(NumericUpDown field, ICommand? value) => field.SetValue(IncreaseProperty, value);

    public static ICommand? GetDecrease(NumericUpDown field) => field.GetValue(DecreaseProperty);

    public static void SetDecrease(NumericUpDown field, ICommand? value) => field.SetValue(DecreaseProperty, value);

    private static void OnSpin(ButtonSpinner spinner, SpinEventArgs e)
    {
        // Same conditions under which the NumericUpDown itself would spin (wheel only while it has the focus).
        if (spinner.TemplatedParent is not NumericUpDown field || !field.AllowSpin || field.IsReadOnly
            || (e.UsingMouseWheel && !field.IsKeyboardFocusWithin))
        {
            return;
        }

        var command = e.Direction == SpinDirection.Increase ? GetIncrease(field) : GetDecrease(field);
        if (command is null)
        {
            return;
        }

        e.Handled = true;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
