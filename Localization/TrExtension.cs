using Avalonia.Markup.Xaml;

namespace ChastiMaid.Localization;

/// <summary>
///     Translates a static XAML label: <c>{l:Tr 'Source text'}</c>, or <c>{l:Tr 'Start', Context='button'}</c>
///     when the same English text needs different translations. Resolved once, when the view is built.
/// </summary>
public class TrExtension(string text) : MarkupExtension
{
    public string? Context { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Context is null
            ? Localizer.Current.GetString(text)
            : Localizer.Current.GetParticularString(Context, text);
    }
}
