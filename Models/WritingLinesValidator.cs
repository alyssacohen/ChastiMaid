namespace ChastiMaid.Models;

public enum WritingLinesValidationResult
{
    Continue,
    LineComplete,
    Mistake,
}

/// <summary>Pure character-by-character validation of a Writing Lines attempt against its target sentence.</summary>
public static class WritingLinesValidator
{
    public static WritingLinesValidationResult Validate(string sentence, string typed)
    {
        if (typed.Length == 0)
        {
            return WritingLinesValidationResult.Continue;
        }

        if (typed == sentence)
        {
            return WritingLinesValidationResult.LineComplete;
        }

        return sentence.StartsWith(typed, StringComparison.Ordinal)
            ? WritingLinesValidationResult.Continue
            : WritingLinesValidationResult.Mistake;
    }

    /// <summary>Splits a mistyped attempt into the correct prefix and the first mismatched character,
    /// for display in the history panel.</summary>
    public static (string CorrectPart, string ErrorChar) SplitAtMismatch(string sentence, string typed)
    {
        var commonLength = 0;
        while (commonLength < typed.Length && commonLength < sentence.Length && typed[commonLength] == sentence[commonLength])
        {
            commonLength++;
        }

        var errorChar = commonLength < typed.Length ? typed[commonLength].ToString() : "";
        return (typed[..commonLength], errorChar);
    }
}
