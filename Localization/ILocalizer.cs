namespace ChastiMaid.Localization;

/// <summary>
///     Translates English source text into the active language. Method names mirror GetText's catalog
///     so that GetText.NET.Extractor finds every call when generating <c>locale/ChastiMaid.pot</c>:
///     the text arguments must be string literals.
/// </summary>
public interface ILocalizer
{
    string GetString(string text);

    string GetString(string text, params object[] args);

    /// <summary>Picks the plural form for <paramref name="n" />; <paramref name="args" /> fill the placeholders.</summary>
    string GetPluralString(string text, string pluralText, long n, params object[] args);

    /// <summary>Same as <see cref="GetString(string)" />, for a text whose translation depends on <paramref name="context" /> (msgctxt).</summary>
    string GetParticularString(string context, string text);

    string GetParticularString(string context, string text, params object[] args);
}
