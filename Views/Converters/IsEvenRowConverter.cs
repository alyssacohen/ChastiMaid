using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace ChastiMaid.Views.Converters;

/// <summary>
/// Converts IsEvenRow bool to CardBackgroundBrush (true) or PageBackgroundBrush (false)
/// by looking up the brush from Application resources at runtime.
/// </summary>
public class IsEvenRowConverter : IValueConverter
{
    public static readonly IsEvenRowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "CardBackgroundBrush" : "PageBackgroundBrush";
        if (Application.Current?.Resources.TryGetResource(key, ThemeVariant.Default, out var resource) == true)
            return resource;
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
