using Avalonia.Markup.Xaml;
using ChastiMaid.Localization;

namespace ChastiMaid.Converters;

public class TrExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Resources.Get(key);
    }
}
