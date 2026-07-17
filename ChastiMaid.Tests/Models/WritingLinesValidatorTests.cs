using ChastiMaid.Models;

namespace ChastiMaid.Tests.Models;

public class WritingLinesValidatorTests
{
    private const string Sentence = "I shall be punctual.";

    [Fact]
    public void Validate_EmptyInput_Continues()
    {
        Assert.Equal(WritingLinesValidationResult.Continue, WritingLinesValidator.Validate(Sentence, ""));
    }

    [Fact]
    public void Validate_CorrectPrefix_Continues()
    {
        Assert.Equal(WritingLinesValidationResult.Continue, WritingLinesValidator.Validate(Sentence, "I shall"));
    }

    [Fact]
    public void Validate_ExactMatch_ReturnsLineComplete()
    {
        Assert.Equal(WritingLinesValidationResult.LineComplete, WritingLinesValidator.Validate(Sentence, Sentence));
    }

    [Fact]
    public void Validate_WrongCharacter_ReturnsMistake()
    {
        Assert.Equal(WritingLinesValidationResult.Mistake, WritingLinesValidator.Validate(Sentence, "I shell"));
    }

    [Fact]
    public void Validate_OverlongInput_ReturnsMistake()
    {
        Assert.Equal(WritingLinesValidationResult.Mistake,
            WritingLinesValidator.Validate(Sentence, Sentence + " extra"));
    }
}
