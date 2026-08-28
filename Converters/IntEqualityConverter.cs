using System.Globalization;
using Avalonia.Data.Converters;

namespace ChastiMaid.Converters;

public class IntEqualityConverter : IValueConverter
{
    public static IntEqualityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int v && parameter is string s && int.TryParse(s, out var target) && v == target;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
