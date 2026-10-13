using System.Globalization;
using ChastiMaid.Services;
using GetText;

namespace ChastiMaid.Localization;

/// <summary>
///     <see cref="ILocalizer" /> over the compiled gettext catalogs in
///     <c>Content/locale/{language}/LC_MESSAGES/ChastiMaid.mo</c>. English is the source language and needs
///     no catalog; a missing catalog or a missing entry falls back to the English text.
/// </summary>
public sealed class Localizer : ILocalizer
{
    private ICatalog _catalog = new Catalog(CultureInfo.InvariantCulture);

    /// <summary>
    ///     The application's localizer. Static access is reserved for code that cannot receive it
    ///     (markup extensions, converters, static helpers); ViewModels get it from <c>MainWindowViewModel</c>.
    /// </summary>
    public static Localizer Current { get; } = new();

    public static string CatalogPath(string language) =>
        Path.Combine(AppPaths.Root, "Content", "locale", language, "LC_MESSAGES", "ChastiMaid.mo");

    /// <summary>Switches to <paramref name="language" /> (a two-letter code); takes effect on the next lookup.</summary>
    public void SetLanguage(string language)
    {
        var culture = new CultureInfo(language);
        var path = CatalogPath(language);
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                _catalog = new Catalog(stream, culture);
                return;
            }
        }
        catch (Exception)
        {
            // An unreadable or corrupt catalog must not stop the app from starting: fall back to English.
        }

        _catalog = new Catalog(culture);
    }

    public string GetString(string text) => _catalog.GetString(text);

    public string GetString(string text, params object[] args) => _catalog.GetString(text, args);

    public string GetPluralString(string text, string pluralText, long n, params object[] args) =>
        _catalog.GetPluralString(text, pluralText, n, args);

    public string GetParticularString(string context, string text) => _catalog.GetParticularString(context, text);

    public string GetParticularString(string context, string text, params object[] args) =>
        _catalog.GetParticularString(context, text, args);
}
