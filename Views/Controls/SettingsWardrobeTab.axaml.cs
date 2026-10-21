using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

/// <summary>
///     Settings › Wardrobe (master/detail). Only focus and keyboard handling live here: the rename box, the dialog
///     fields and the number fields; every decision is the <see cref="WardrobeViewModel" />'s.
/// </summary>
public partial class SettingsWardrobeTab : UserControl
{
    private IInputElement? _focusBeforeDialog;
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

    /// <summary>Esc closes the topmost Wardrobe dialog (the confirmation dialog handles its own Esc).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _vm is not null && _vm.Confirmation is null)
        {
            if (_vm.AddEntryKind is not null)
            {
                _vm.CancelAddEntryCommand.Execute(null);
                e.Handled = true;
            }
            else if (_vm.ManageKind is not null)
            {
                _vm.CloseManageCommand.Execute(null);
                e.Handled = true;
            }
            else if (_vm.IsRulesDialogOpen)
            {
                _vm.CloseRulesCommand.Execute(null);
                e.Handled = true;
            }
        }

        base.OnKeyDown(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WardrobeViewModel.EditingItem) when _vm?.EditingItem is not null:
                FocusRenameBox();
                break;
            case nameof(WardrobeViewModel.AddEntryKind):
                OnDialogToggled(_vm?.AddEntryKind is not null, EntryBox);
                break;
            case nameof(WardrobeViewModel.ManageKind):
                OnDialogToggled(_vm?.ManageKind is not null, ManageBox);
                break;
            case nameof(WardrobeViewModel.IsRulesDialogOpen):
                OnDialogToggled(_vm?.IsRulesDialogOpen == true, RulesCloseButton);
                break;
        }
    }

    /// <summary>Focus moves into a dialog when it opens and back to where it was when it closes.</summary>
    private void OnDialogToggled(bool open, Control target)
    {
        if (open)
        {
            _focusBeforeDialog = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Dispatcher.UIThread.Post(() =>
            {
                target.Focus(NavigationMethod.Tab);
                (target as TextBox)?.SelectAll();
            }, DispatcherPriority.Loaded);
            return;
        }

        var previous = _focusBeforeDialog;
        _focusBeforeDialog = null;
        Dispatcher.UIThread.Post(() => previous?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
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

    // ── Dialog fields, number fields, popovers ───────────────────────────

    private void OnEntryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm is null)
        {
            return;
        }

        if (ReferenceEquals(sender, ManageBox))
        {
            _vm.ManageAdd();
        }
        else
        {
            _vm.ConfirmAddEntry();
        }

        e.Handled = true;
    }

    /// <summary>Whatever was typed and not applied (an empty field, a refused separator) gives way to the real value.</summary>
    private void OnNumberLostFocus(object? sender, RoutedEventArgs e)
    {
        ((sender as Control)?.DataContext as WardrobeNumberFieldViewModel)?.EndEdit();
    }

    /// <summary>The Manage dialogs open from the filter popover, which must not stay on top of them.</summary>
    private void OnManageClick(object? sender, RoutedEventArgs e)
    {
        FilterButton.Flyout?.Hide();
    }
}
