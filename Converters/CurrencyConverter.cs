using System.Globalization;
using Avalonia.Data.Converters;

namespace ChastiMaid.Converters;

public class CurrencyConverter : IValueConverter
{
    public static CurrencyConverter Instance { get; } = new();

    public static string Format(decimal value) => $"£{value.ToString("F2", CultureInfo.InvariantCulture)}";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            decimal d => Format(d),
            double d => Format((decimal)d),
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
