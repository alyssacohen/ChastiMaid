using System.Globalization;
using System.Text;

namespace ChastiMaid.Services;

/// <summary>
///     Compares a transcribed phrase against the target phrase it was supposed to match. Character-level
///     Levenshtein on normalized text, not word-level/WER — a short quote (a handful of words) needs finer
///     granularity than word-level quantization (12.5% steps for an 8-word phrase) can give near an 80-85%
///     threshold.
/// </summary>
public static class SpeechTextMatcher
{
    /// <summary>Lowercases, strips diacritics and punctuation, and collapses whitespace.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(' ');
            }
        }

        var words = builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words);
    }

    /// <summary>
    ///     Similarity between two already-arbitrary strings, 0..1, as <c>1 - Levenshtein / max(len(a), len(b))</c>
    ///     over their normalized forms. Two empty normalized strings are treated as identical (1.0).
    /// </summary>
    public static double Similarity(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);

        if (normalizedA.Length == 0 && normalizedB.Length == 0)
        {
            return 1.0;
        }

        var distance = LevenshteinDistance(normalizedA, normalizedB);
        var maxLength = Math.Max(normalizedA.Length, normalizedB.Length);
        return 1.0 - distance / (double)maxLength;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var previousRow = new int[b.Length + 1];
        var currentRow = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previousRow[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            currentRow[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[b.Length];
    }
}
