using Avalonia.Markup.Xaml;

namespace ChastiMaid.Converters;

public class UpperCaseExtension(object value) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return value.ToString()?.ToUpperInvariant() ?? "";
    }
}
