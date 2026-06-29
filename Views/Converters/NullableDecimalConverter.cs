using System.Globalization;
using Avalonia.Data.Converters;

namespace ChastiMaid.Views.Converters;

/// <summary>
///     Bridges decimal? (NumericUpDown.Value) ↔ double (ViewModel properties).
///     ConvertBack maps null to the minimum passed as ConverterParameter (default 0).
/// </summary>
public class NullableDecimalConverter : IValueConverter
{
    public static NullableDecimalConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is double d ? (decimal)d : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            return (double)d;
        }

        var min = parameter is string s &&
                  double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m)
            ? m
            : 0.0;
        return min;
    }
}
