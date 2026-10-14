using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Dialogs;

public partial class ConfirmDialog : UserControl
{
    public ConfirmDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ConfirmDialogViewModel { CancelIsDefault: true, HasCancel: true })
            {
                // Keyboard-style focus, so the focus ring shows which button Enter/Space would press.
                Dispatcher.UIThread.Post(() => CancelButton.Focus(NavigationMethod.Tab));
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is ConfirmDialogViewModel { HasCancel: true } dialog)
        {
            dialog.CancelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
