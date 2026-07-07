using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ChastiMaid.Views.Converters;

/// <summary>Resolves a DynamicResource brush key string (e.g. "BrushStatusIdle") to its IBrush.</summary>
public class ResourceBrushKeyConverter : IValueConverter
{
    public static ResourceBrushKeyConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string key && Application.Current!.TryGetResource(key, null, out var resource)
            ? resource as IBrush
            : Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
