using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class DashboardView : UserControl
{
    private TopLevel? _topLevel;

    public DashboardView()
    {
        InitializeComponent();

        // Wire OutfitDrawer CloseRequested → close the drawer
        OutfitDrawerControl.CloseRequested += (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.IsOutfitDrawerOpen = false;
            }
        };

        // Mirror Bounds.Width to IsLandscape on the ViewModel
        this.GetObservable(BoundsProperty).Subscribe(bounds =>
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.IsLandscape = bounds.Width > 900;
            }
        });

        // Escape must dismiss the popup outcome regardless of what currently has keyboard focus,
        // so it is wired at the window level (tunnel) rather than as a KeyBinding on this control.
        AttachedToVisualTree += (_, _) =>
        {
            _topLevel = TopLevel.GetTopLevel(this);
            _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _topLevel = null;
        };
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is DashboardViewModel vm)
        {
            vm.DismissPopupOutcomeCommand.Execute(null);
        }
    }

    // Clicking the popup backdrop dismisses the outcome screen early (question/reminder screen ignores it, see VM guard)
    private void OnPopupOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.DismissPopupOutcomeCommand.Execute(null);
        }
    }

    // Prevent clicks inside the dialog box from bubbling up to the backdrop handler
    private void OnPopupDialogBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
