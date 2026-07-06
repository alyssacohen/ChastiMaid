using System.Globalization;
using Avalonia.Data.Converters;

namespace ChastiMaid.Views.Converters;

/// <summary>
///     Bridges NumericUpDown's decimal? Value with non-nullable numeric ViewModel properties
///     (int, decimal or double). ConvertBack maps a cleared field (null) to the minimum passed
///     via ConverterParameter (default 0), avoiding InvalidCastException on non-nullable targets.
/// </summary>
public class NullableNumericConverter : IValueConverter
{
    public static NullableNumericConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => null,
            decimal d => d,
            _ => System.Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        var result = value is decimal d
            ? d
            : parameter is string s
                ? decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)
                : 0m;

        return System.Convert.ChangeType(result, underlying, CultureInfo.InvariantCulture);
    }
}
