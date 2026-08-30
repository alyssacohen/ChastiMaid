using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

public partial class SettingsTestOutfitOverlay : UserControl
{
    private TopLevel? _topLevel;

    public SettingsTestOutfitOverlay()
    {
        InitializeComponent();

        // Escape must dismiss the popup regardless of what currently has keyboard focus,
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
        if (e.Key == Key.Escape && DataContext is SettingsViewModel vm)
        {
            vm.CloseTestOutfitDialogCommand.Execute(null);
        }
    }

    // Clicking the popup backdrop dismisses it
    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.CloseTestOutfitDialogCommand.Execute(null);
        }
    }

    // Prevent clicks inside the dialog box from bubbling up to the backdrop handler
    private void OnDialogBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
