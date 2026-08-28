using Avalonia;
using Avalonia.Controls;

namespace ChastiMaid.Views.Controls;

/// <summary>
///     A WrapPanel that treats one designated child (TrailingItem) as always-last:
///     chips wrap normally; the trailing TextBox takes remaining row width or a new row.
/// </summary>
public class WrapPanelWithTrailingItem : Panel
{
    public Control? TrailingItem { get; set; }
    public double HorizontalSpacing { get; set; } = 4;
    public double VerticalSpacing { get; set; } = 4;
    public double TrailingMinWidth { get; set; } = 120;

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = double.IsInfinity(availableSize.Width);
        var avw = infinite ? 0.0 : availableSize.Width;
        double x = 0, rowH = 0, totalH = 0;

        foreach (var child in Children)
        {
            if (child == TrailingItem || !child.IsVisible)
            {
                continue;
            }

            child.Measure(availableSize);
            var cw = child.DesiredSize.Width;
            var gap = x > 0 ? HorizontalSpacing : 0;
            if (!infinite && x > 0 && x + gap + cw > avw)
            {
                totalH += rowH + VerticalSpacing;
                x = 0;
                rowH = 0;
                gap = 0;
            }

            x += gap + cw;
            rowH = Math.Max(rowH, child.DesiredSize.Height);
        }

        if (TrailingItem is { IsVisible: true })
        {
            var gap = x > 0 ? HorizontalSpacing : 0;
            if (infinite)
            {
                TrailingItem.Measure(availableSize);
            }
            else
            {
                var remaining = avw - x - gap;
                if (x > 0 && remaining < TrailingMinWidth)
                {
                    totalH += rowH + VerticalSpacing;
                    x = 0;
                    rowH = 0;
                    gap = 0;
                    TrailingItem.Measure(new Size(avw, availableSize.Height));
                }
                else
                {
                    TrailingItem.Measure(new Size(Math.Max(remaining, TrailingMinWidth), availableSize.Height));
                }
            }

            x += gap + TrailingItem.DesiredSize.Width;
            rowH = Math.Max(rowH, TrailingItem.DesiredSize.Height);
        }

        totalH += rowH;
        return new Size(infinite ? x : avw, totalH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, rowH = 0;

        foreach (var child in Children)
        {
            if (child == TrailingItem || !child.IsVisible)
            {
                continue;
            }

            var cw = child.DesiredSize.Width;
            var gap = x > 0 ? HorizontalSpacing : 0;
            if (x > 0 && x + gap + cw > finalSize.Width)
            {
                y += rowH + VerticalSpacing;
                x = 0;
                rowH = 0;
                gap = 0;
            }

            child.Arrange(new Rect(x + gap, y, cw, child.DesiredSize.Height));
            x += gap + cw;
            rowH = Math.Max(rowH, child.DesiredSize.Height);
        }

        if (TrailingItem is { IsVisible: true })
        {
            var gap = x > 0 ? HorizontalSpacing : 0;
            var remaining = finalSize.Width - x - gap;
            if (x > 0 && remaining < TrailingMinWidth)
            {
                y += rowH + VerticalSpacing;
                x = 0;
                rowH = 0;
                TrailingItem.Arrange(new Rect(0, y, finalSize.Width, TrailingItem.DesiredSize.Height));
            }
            else
            {
                var tw = Math.Max(remaining, 0.0);
                TrailingItem.Arrange(new Rect(x + gap, y, tw, TrailingItem.DesiredSize.Height));
            }

            rowH = Math.Max(rowH, TrailingItem.DesiredSize.Height);
        }

        return new Size(finalSize.Width, y + rowH);
    }
}
