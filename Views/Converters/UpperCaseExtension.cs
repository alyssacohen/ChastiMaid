using Avalonia.Markup.Xaml;

namespace ChastiMaid.Views.Converters;

public class UpperCaseExtension(object value) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return value.ToString()?.ToUpperInvariant() ?? "";
    }
}
