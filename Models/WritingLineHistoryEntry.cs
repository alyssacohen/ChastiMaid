namespace ChastiMaid.Models;

/// <summary>A completed or abandoned Writing Lines attempt, kept for the history panel.</summary>
public record WritingLineHistoryEntry(string CorrectPart, string ErrorChar)
{
    public bool IsSuccess => ErrorChar.Length == 0;
}
