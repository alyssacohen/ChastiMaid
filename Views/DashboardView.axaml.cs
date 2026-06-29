using Avalonia;
using Avalonia.Controls;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class DashboardView : UserControl
{
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
    }
}
