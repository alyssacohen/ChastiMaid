using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

/// <summary>
///     Settings › Wardrobe (master/detail). Only focus and keyboard handling live here: the rename box, the number
///     fields and the touch cells; every decision is the <see cref="WardrobeViewModel" />'s. Its dialogs are
///     <see cref="WardrobeDialogs" />.
/// </summary>
public partial class SettingsWardrobeTab : UserControl
{
    private WardrobeViewModel? _vm;

    public SettingsWardrobeTab()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.RenameRejected -= OnRenameRejected;
        }

        _vm = DataContext as WardrobeViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.RenameRejected += OnRenameRejected;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WardrobeViewModel.EditingItem) && _vm?.EditingItem is not null)
        {
            FocusRenameBox();
        }
    }

    // ── Rename ───────────────────────────────────────────────────────────

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: WardrobeEditViewModel item })
        {
            _vm?.BeginRename(item);
        }
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _vm?.CommitRename();
                e.Handled = true;
                break;
            case Key.Escape:
                _vm?.CancelRename();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Leaving the box commits; a refused name keeps the box focused with its text selected.</summary>
    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { IsVisible: true })
        {
            _vm?.CommitRename();
        }
    }

    private void OnRenameRejected(object? sender, EventArgs e)
    {
        FocusRenameBox();
    }

    /// <summary>Only the row being renamed shows its box, so the visible one is the right one.</summary>
    private void FocusRenameBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var box = this.GetVisualDescendants().OfType<TextBox>()
                .FirstOrDefault(t => t.Name == "RenameBox" && t.IsEffectivelyVisible);
            box?.Focus(NavigationMethod.Tab);
            box?.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    // ── Number fields, popovers, touch cells ─────────────────────────────

    /// <summary>Whatever was typed and not applied (an empty field, a refused separator) gives way to the real value.</summary>
    private void OnNumberLostFocus(object? sender, RoutedEventArgs e)
    {
        ((sender as Control)?.DataContext as WardrobeNumberFieldViewModel)?.EndEdit();
    }

    /// <summary>
    ///     The Manage dialogs open from the filter popover, which must not stay on top of them. Hidden after the click,
    ///     not during it: a hidden popover loses its DataContext and the button's command would never run.
    /// </summary>
    private void OnManageClick(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => FilterButton.Flyout?.Hide());
    }

    /// <summary>A tap in a checkbox's touch cell but outside the box toggles it like a click on the box would.</summary>
    private void OnCheckCellTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Panel { Children: [CheckBox box] } && (e.Source as Visual)?.FindAncestorOfType<CheckBox>(true) is null)
        {
            (ControlAutomationPeer.CreatePeerForElement(box) as IToggleProvider)?.Toggle();
        }
    }
}
