using System.Globalization;
using Avalonia.Data.Converters;
using ChastiMaid.Localization;

namespace ChastiMaid.Converters;

/// <summary>
///     Formats a CurrentDebt-shaped decimal (positive = debt owed, negative = accumulated credit) using
///     Math.Abs and whichever resource key matches its sign. ConverterParameter is
///     "&lt;DebtResourceKey&gt;|&lt;CreditResourceKey&gt;" (each resource string takes {0} as the
///     amount, or none for a plain label), with an optional trailing "|upper" segment to uppercase the
///     result for LabelUI-style bindings that can't use the static {conv:UpperCase} extension.
/// </summary>
public class BalanceLabelConverter : IValueConverter
{
    public static BalanceLabelConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal balance || parameter is not string keys)
        {
            return value?.ToString() ?? string.Empty;
        }

        var parts = keys.Split('|');
        var key = balance < 0 ? parts[1] : parts[0];
        var text = string.Format(CultureInfo.InvariantCulture, Resources.Get(key), Math.Abs(balance));
        return parts is [_, _, "upper"] ? text.ToUpperInvariant() : text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
