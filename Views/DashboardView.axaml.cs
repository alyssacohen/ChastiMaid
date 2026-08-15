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

        // Mirror Bounds.Width to IsLandscape, and Bounds.Height to the idle-popup responsive
        // sizing (shared by every popup with a live webcam preview), on the ViewModel —
        // recalculated on every resize so the popup never overflows the window regardless of
        // orientation.
        this.GetObservable(BoundsProperty).Subscribe(bounds =>
        {
            if (DataContext is not DashboardViewModel vm)
            {
                return;
            }

            vm.IsLandscape = bounds.Width > 900;

            const double verticalMargin = 64; // breathing room from the window's top/bottom edges
            const double chromeReserve = 300; // countdown/instructions/button/camera-selector, padding, spacing
            const double previewAspect = 320.0 / 480.0; // width/height, matches the requested crop ratio

            var dialogMaxHeight = Math.Max(360, bounds.Height - verticalMargin);
            var previewHeight = Math.Clamp(dialogMaxHeight - chromeReserve, 240, 640);

            vm.PopupDialogMaxHeight = dialogMaxHeight;
            vm.PopupPreviewHeight = previewHeight;
            vm.PopupPreviewWidth = previewHeight * previewAspect;
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
            vm.DismissChoreResultCommand.Execute(null);
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

    // Clicking the chore-result backdrop dismisses it early (VM guards against no-op when already hidden)
    private void OnChoreResultOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.DismissChoreResultCommand.Execute(null);
        }
    }

    // Prevent clicks inside the dialog box from bubbling up to the backdrop handler
    private void OnChoreResultDialogBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
