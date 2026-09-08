using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ChastiMaid.Converters;

/// <summary>
///     Colors a CurrentDebt-shaped decimal: BrushEmergencyText (red) for debt owed, BrushTypingCorrect
///     (green) for accumulated credit, TextPrimary when settled at exactly zero.
/// </summary>
public class BalanceBrushConverter : IValueConverter
{
    public static BalanceBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            decimal balance when balance > 0 => "BrushEmergencyText",
            decimal balance when balance < 0 => "BrushTypingCorrect",
            _ => "TextPrimary",
        };

        return Application.Current!.TryGetResource(key, null, out var brush) ? brush as IBrush : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
