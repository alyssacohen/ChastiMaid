using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

/// <summary>
///     The Wardrobe's dialogs (Add and Manage slots/tags, confirmations) and notice, over the whole Settings screen.
///     Only focus and keys live here; every decision is the <see cref="WardrobeViewModel" />'s.
/// </summary>
public partial class WardrobeDialogs : UserControl
{
    private IInputElement? _focusBeforeDialog;
    private WardrobeViewModel? _vm;

    public WardrobeDialogs()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PropertyChanging -= OnViewModelPropertyChanging;
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as WardrobeViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanging += OnViewModelPropertyChanging;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>Esc closes the topmost dialog (the confirmation dialog handles its own Esc).</summary>
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
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    ///     Remembers the focused control before a dialog opens: once it is open the background is disabled and drops
    ///     its focus, too late to read it then.
    /// </summary>
    private void OnViewModelPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName is nameof(WardrobeViewModel.AddEntryKind) or nameof(WardrobeViewModel.ManageKind)
            && _vm is { AddEntryKind: null, ManageKind: null })
        {
            _focusBeforeDialog = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WardrobeViewModel.AddEntryKind):
                OnDialogToggled(_vm?.AddEntryKind is not null, EntryBox);
                break;
            case nameof(WardrobeViewModel.ManageKind):
                OnDialogToggled(_vm?.ManageKind is not null, ManageBox);
                break;
        }
    }

    /// <summary>Focus moves into a dialog when it opens and back to where it was when it closes.</summary>
    private void OnDialogToggled(bool open, Control target)
    {
        if (open)
        {
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
}
