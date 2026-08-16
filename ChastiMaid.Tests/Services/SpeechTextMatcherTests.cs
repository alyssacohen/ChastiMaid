using ChastiMaid.Services;

namespace ChastiMaid.Tests.Services;

public class SpeechTextMatcherTests
{
    private const string Sentence = "The mistress expects punctuality, not excuses.";

    [Fact]
    public void Similarity_IdenticalStrings_ReturnsOne()
    {
        Assert.Equal(1.0, SpeechTextMatcher.Similarity(Sentence, Sentence));
    }

    [Fact]
    public void Similarity_CompletelyDifferentStrings_IsNearZero()
    {
        Assert.True(SpeechTextMatcher.Similarity(Sentence, "xyz qwk zzz") < 0.3);
    }

    [Fact]
    public void Similarity_CaseAccentAndPunctuationDifferences_ReturnsOne()
    {
        Assert.Equal(1.0,
            SpeechTextMatcher.Similarity(Sentence, "THE MISTRESS expects, punctuality not excuses"));
    }

    [Fact]
    public void Similarity_SinglePlausibleTranscriptionError_IsAboveThreshold()
    {
        var transcribed = "the mistress expect punctuality not excuses";
        Assert.True(SpeechTextMatcher.Similarity(Sentence, transcribed) >= 0.82);
    }

    [Fact]
    public void Similarity_MultipleErrorsAndMissingWords_IsBelowThreshold()
    {
        var transcribed = "the mistress punctuality";
        Assert.True(SpeechTextMatcher.Similarity(Sentence, transcribed) < 0.82);
    }

    [Fact]
    public void Similarity_BothEmpty_ReturnsOne()
    {
        Assert.Equal(1.0, SpeechTextMatcher.Similarity("", "   "));
    }

    [Fact]
    public void Normalize_AccentsAndPunctuation_AreStripped()
    {
        Assert.Equal("perche non hai finito", SpeechTextMatcher.Normalize("Perché, non hai finito?!"));
    }
}
