using System.Globalization;
using Avalonia.Data.Converters;

namespace ChastiMaid.Converters;

public class UpperCaseConverter : IValueConverter
{
    public static UpperCaseConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s ? s.ToUpperInvariant() : value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
