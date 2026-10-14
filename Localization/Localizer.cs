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
    private Catalog _catalog = new(CultureInfo.InvariantCulture);

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

    /// <summary>
    ///     Marks <paramref name="text" /> for extraction without translating it, for texts kept in a table and
    ///     translated later with <see cref="T(string)" />.
    /// </summary>
    public static string Mark(string text) => text;

    // The *Default lookups return the raw translation: GetText's other overloads already run string.Format
    // (the plural ones with n as the only argument), which breaks texts with more than one placeholder.
    public string T(string text) => _catalog.GetStringDefault(text, text);

    public string T(string text, params object[] args) => Format(T(text), args);

    public string N(string text, string pluralText, long n, params object[] args) =>
        Format(_catalog.GetPluralStringDefault(text, text, pluralText, n), args);

    // gettext keys a message with a context as "context\u0004msgid".
    public string P(string context, string text) => _catalog.GetStringDefault(context + "\u0004" + text, text);

    public string P(string context, string text, params object[] args) => Format(P(context, text), args);

    // Invariant on purpose: amounts keep a decimal point in every language (GetText would use the catalog's culture).
    private static string Format(string format, object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
