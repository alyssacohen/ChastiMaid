namespace ChastiMaid.Localization;

/// <summary>
///     Translates English source text into the active language. The text arguments must be plain string
///     literals (no interpolation, no <c>cond ? "a" : "b"</c>): the extractor that generates
///     <c>locale/ChastiMaid.pot</c> only sees literals. Placeholders are filled with the invariant culture.
/// </summary>
public interface ILocalizer
{
    /// <summary>Translates <paramref name="text" />.</summary>
    string T(string text);

    string T(string text, params object[] args);

    /// <summary>Plural: picks the form for <paramref name="n" />; <paramref name="args" /> fill the placeholders.</summary>
    string N(string text, string pluralText, long n, params object[] args);

    /// <summary>Same as <see cref="T(string)" />, for a text whose translation depends on <paramref name="context" /> (msgctxt).</summary>
    string P(string context, string text);

    string P(string context, string text, params object[] args);
}
