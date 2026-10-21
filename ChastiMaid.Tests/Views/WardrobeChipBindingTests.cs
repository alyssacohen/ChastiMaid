using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Tests.Views;

/// <summary>A real ToggleButton bound the way the Wardrobe chips are: IsChecked one way, the click through the command.</summary>
public class WardrobeChipBindingTests
{
    private static (ToggleButton Toggle, IToggleProvider Peer) Bind(WardrobeChipViewModel chip)
    {
        var toggle = new ToggleButton { DataContext = chip };
        toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(WardrobeChipViewModel.IsChecked)) { Mode = BindingMode.OneWay });
        toggle.Bind(ToggleButton.CommandProperty, new Binding(nameof(WardrobeChipViewModel.ToggleCommand)));
        return (toggle, (IToggleProvider)ControlAutomationPeer.CreatePeerForElement(toggle));
    }

    [Fact]
    public void ARefusedClick_PutsTheToggleButtonBackOnTheViewModelState()
    {
        var chip = new WardrobeChipViewModel("Base", () => { }); // the click is refused: nothing changes
        chip.SetCount(1, 1);
        var (toggle, peer) = Bind(chip);
        Assert.True(toggle.IsChecked);

        peer.Toggle();

        Assert.True(toggle.IsChecked);
    }

    [Fact]
    public void APartialChip_IsIndeterminateForAccessibility_AndAClickFollowsTheViewModel()
    {
        WardrobeChipViewModel chip = null!;
        chip = new WardrobeChipViewModel("wool", () => chip.SetCount(2, 2)); // a partial click adds to every target
        chip.SetCount(1, 2);
        var (toggle, peer) = Bind(chip);
        Assert.Null(toggle.IsChecked);
        Assert.Equal(ToggleState.Indeterminate, peer.ToggleState);

        peer.Toggle();

        Assert.True(toggle.IsChecked);
        Assert.Equal(ToggleState.On, peer.ToggleState);
    }
}
