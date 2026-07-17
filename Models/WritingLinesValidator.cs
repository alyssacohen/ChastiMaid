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
}
