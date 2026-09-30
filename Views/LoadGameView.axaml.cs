using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class LoadGameView : UserControl
{
    private TopLevel? _topLevel;

    public LoadGameView()
    {
        InitializeComponent();

        // Escape closes the contract overlay regardless of what has keyboard focus, so it is wired at the
        // window level (tunnel) rather than as a KeyBinding on this control.
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
        if (e.Key == Key.Escape && DataContext is LoadGameViewModel { ContractClauses: not null } vm)
        {
            vm.CloseContractCommand.Execute(null);
            e.Handled = true;
        }
    }
}
