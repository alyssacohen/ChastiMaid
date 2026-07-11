using ChastiMaid.Models;

namespace ChastiMaid.Tests.Models;

public class ImpossibleQuestionBankTests
{
    [Fact]
    public void GetRandom_AlwaysReturnsQuestionFromKnownList()
    {
        for (var i = 0; i < 100; i++)
        {
            var question = ImpossibleQuestionBank.GetRandom();

            Assert.False(string.IsNullOrEmpty(question.QuestionKey));
            Assert.False(string.IsNullOrEmpty(question.YesPositiveKey));
            Assert.False(string.IsNullOrEmpty(question.YesNegativeKey));
            Assert.False(string.IsNullOrEmpty(question.NoPositiveKey));
            Assert.False(string.IsNullOrEmpty(question.NoNegativeKey));
        }
    }

    [Fact]
    public void RollOutcome_WithSameSeed_IsDeterministic()
    {
        var first = ImpossibleQuestionBank.RollOutcome(new Random(42));
        var second = ImpossibleQuestionBank.RollOutcome(new Random(42));

        Assert.Equal(first, second);
    }

    [Fact]
    public void RollOutcome_OverManyIterations_ProducesBothOutcomes()
    {
        var outcomes = Enumerable.Range(0, 200)
            .Select(seed => ImpossibleQuestionBank.RollOutcome(new Random(seed)))
            .ToList();

        Assert.Contains(true, outcomes);
        Assert.Contains(false, outcomes);
    }
}
