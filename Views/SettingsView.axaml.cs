using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class SettingsView : UserControl
{
    private IInputElement? _focusBeforeRules;
    private WardrobeViewModel? _wardrobe;

    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_wardrobe is not null)
        {
            _wardrobe.PropertyChanged -= OnWardrobeChanged;
        }

        _wardrobe = (DataContext as SettingsViewModel)?.Wardrobe;
        if (_wardrobe is not null)
        {
            _wardrobe.PropertyChanged += OnWardrobeChanged;
        }
    }

    /// <summary>Focus moves into the rules dialog when it opens and back to where it was when it closes.</summary>
    private void OnWardrobeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WardrobeViewModel.IsRulesDialogOpen) || _wardrobe is null)
        {
            return;
        }

        if (_wardrobe.IsRulesDialogOpen)
        {
            _focusBeforeRules = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Dispatcher.UIThread.Post(() => RulesCloseButton.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
            return;
        }

        var previous = _focusBeforeRules;
        _focusBeforeRules = null;
        Dispatcher.UIThread.Post(() => previous?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }
}
