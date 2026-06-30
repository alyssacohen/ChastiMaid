using Avalonia.Markup.Xaml;

namespace ChastiMaid.Views.Converters;

public class UpperCaseExtension(string value) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return value.ToUpperInvariant();
    }
}
