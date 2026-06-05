using Avalonia;
using Avalonia.Controls;
using ChastiMaid.ViewModels;
using ReactiveUI;

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
                vm.IsOutfitDrawerOpen = false;
        };

        // Mirror Bounds.Width to IsLandscape on the ViewModel
        this.GetObservable(BoundsProperty).Subscribe(bounds =>
        {
            if (DataContext is DashboardViewModel vm)
                vm.IsLandscape = bounds.Width > 900;
        });

        // Keep OutfitDrawer DataContext in sync with the ViewModel's CurrentOutfitVm
        this.GetObservable(DataContextProperty).Subscribe(ctx =>
        {
            if (ctx is DashboardViewModel vm)
            {
                vm.WhenAnyValue(x => x.CurrentOutfitVm)
                  .Subscribe(outfit => OutfitDrawerControl.DataContext = outfit);
            }
        });
    }
}
