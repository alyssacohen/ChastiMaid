using Avalonia.Controls;
using Avalonia.Input;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

public partial class DashboardIdlePopupOverlay : UserControl
{
    public DashboardIdlePopupOverlay()
    {
        InitializeComponent();
    }

    // Clicking the popup backdrop dismisses the outcome screen early (question/reminder screen ignores it, see VM guard)
    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.DismissPopupOutcomeCommand.Execute(null);
        }
    }

    // Prevent clicks inside the dialog box from bubbling up to the backdrop handler
    private void OnDialogBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
