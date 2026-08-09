using Avalonia;
using Avalonia.Controls;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views;

public partial class FortuneDeckView : UserControl
{
    public FortuneDeckView()
    {
        InitializeComponent();

        // The arc's radius and every card pose's anchor point scale with the draw area's actual measured size
        // so the ribbon never overflows in portrait mode (see docs/design/fortune-deck-ui.md "Orientation and
        // screen size") and stays centered in the visible area.
        DrawArea.GetObservable(BoundsProperty).Subscribe(bounds =>
        {
            if (DataContext is FortuneDeckViewModel vm)
            {
                vm.SetContainerSize(bounds.Width, bounds.Height);
            }
        });
    }
}
