using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class WritingLinesPunishmentView : UserControl
{
    public WritingLinesPunishmentView()
    {
        InitializeComponent();

        CaptureBox.AddHandler(TextInputEvent, OnCaptureTextInput, RoutingStrategies.Tunnel);
        CaptureBox.AddHandler(KeyDownEvent, OnCaptureKeyDown, RoutingStrategies.Tunnel);
        CaptureBox.AddHandler(TextBox.PastingFromClipboardEvent, OnCapturePasting, RoutingStrategies.Tunnel);
        Loaded += (_, _) => CaptureBox.Focus();
    }

    private void OnCaptureTextInput(object? sender, TextInputEventArgs e)
    {
        if (DataContext is WritingLinesPunishmentViewModel vm && !string.IsNullOrEmpty(e.Text))
        {
            vm.HandleCharacter(e.Text);
            SyncCaretToEnd();
        }

        e.Handled = true;
    }

    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                (DataContext as WritingLinesPunishmentViewModel)?.HandleEnter();
                SyncCaretToEnd();
                e.Handled = true;
                break;

            case Key.Back:
                // No undo — a wrong character already clears the line on its own (see spec).
                e.Handled = true;
                break;
        }
    }

    // Paste bypasses TextInputEvent entirely, which would let unvalidated text through the
    // always-a-correct-prefix Text binding — block it, same spirit as the no-backspace/no-undo rule.
    private static void OnCapturePasting(object? sender, RoutedEventArgs e) => e.Handled = true;

    // Text is bound one-way from the ViewModel, so Avalonia won't automatically move the caret to
    // match — push it to the end after every VM-driven update so it always sits right after what
    // was just typed.
    private void SyncCaretToEnd() => CaptureBox.CaretIndex = CaptureBox.Text?.Length ?? 0;
}
