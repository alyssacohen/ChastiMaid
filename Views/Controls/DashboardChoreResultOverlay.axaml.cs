using Avalonia.Controls;
using Avalonia.Input;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

public partial class DashboardChoreResultOverlay : UserControl
{
    public DashboardChoreResultOverlay()
    {
        InitializeComponent();
    }

    // Clicking the backdrop dismisses it early (VM guards against no-op when already hidden)
    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.DismissChoreResultCommand.Execute(null);
        }
    }

    // Prevent clicks inside the dialog box from bubbling up to the backdrop handler
    private void OnDialogBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
