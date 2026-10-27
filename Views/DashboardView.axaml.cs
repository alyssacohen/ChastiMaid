using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ChastiMaid.Services;
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

        // Size the idle popups (shared by every popup with a live webcam preview) from Bounds.Height,
        // recalculated on every resize so the popup never overflows the window.
        this.GetObservable(BoundsProperty).Subscribe(bounds =>
        {
            if (DataContext is not DashboardViewModel vm)
            {
                return;
            }

            const double verticalMargin = 64; // breathing room from the window's top/bottom edges
            const double chromeReserve = 300; // countdown/instructions/button/camera-selector, padding, spacing

            var dialogMaxHeight = Math.Max(360, bounds.Height - verticalMargin);
            var previewHeight = Math.Clamp(dialogMaxHeight - chromeReserve, 240, 640);

            vm.PopupDialogMaxHeight = dialogMaxHeight;
            vm.PopupPreviewHeight = previewHeight;
            vm.PopupPreviewWidth = previewHeight * WebcamPreviewService.CaptureAspectRatio;
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
}
