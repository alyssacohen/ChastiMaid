using System.ComponentModel;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Tests.ViewModels;

public class LinkedRangesTests
{
    private sealed class Pair : INotifyPropertyChanged
    {
        private int _max;
        private int _min;

        public int Min
        {
            get => _min;
            set => Set(ref _min, value, nameof(Min));
        }

        public int Max
        {
            get => _max;
            set => Set(ref _max, value, nameof(Max));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set(ref int field, int value, string name)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private static (Pair Range, LinkedRanges Links) Make(int min, int max)
    {
        var range = new Pair { Min = min, Max = max };
        return (range, new LinkedRanges(range, (nameof(Pair.Min), nameof(Pair.Max))));
    }

    [Fact]
    public void AMaximumLoweredBelowTheMinimum_PushesTheMinimumDown()
    {
        var (range, links) = Make(60, 300);

        range.Max = 40;
        links.Commit();

        Assert.Equal((40, 40), (range.Min, range.Max));
    }

    [Fact]
    public void AMinimumRaisedAboveTheMaximum_PushesTheMaximumUp()
    {
        var (range, links) = Make(60, 300);

        range.Min = 500;
        links.Commit();

        Assert.Equal((500, 500), (range.Min, range.Max));
    }

    [Fact]
    public void AnOrderedPair_IsLeftAlone()
    {
        var (range, links) = Make(60, 300);

        range.Max = 200;
        range.Min = 100;
        links.Commit();

        Assert.Equal((100, 200), (range.Min, range.Max));
    }

    [Fact]
    public void TypingAMaximumDigitByDigit_DoesNotDragTheMinimumThroughThePrefixes()
    {
        var (range, links) = Make(60, 300);

        range.Max = 1;
        range.Max = 15;
        range.Max = 150;
        links.Commit();

        Assert.Equal((60, 150), (range.Min, range.Max));
    }
}
