using System.Globalization;
using Avalonia.Data.Converters;

namespace ChastiMaid.Converters;

/// <summary>
///     Bridges a TextBox's string Text with a non-nullable numeric ViewModel property
///     (int or decimal). ConvertBack maps an empty or unparsable string to the minimum
///     passed via ConverterParameter (default 0), avoiding FormatException on empty input.
/// </summary>
public class NumericTextConverter : IValueConverter
{
    public static NumericTextConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is IFormattable f ? f.ToString(null, culture) : value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        var result = value is string s && decimal.TryParse(s, NumberStyles.Any, culture, out var d)
            ? d
            : parameter is string p
                ? decimal.Parse(p, NumberStyles.Any, CultureInfo.InvariantCulture)
                : 0m;

        return System.Convert.ChangeType(result, underlying, CultureInfo.InvariantCulture);
    }
}
