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

            Assert.False(string.IsNullOrEmpty(question.Question));
            Assert.False(string.IsNullOrEmpty(question.YesPositive));
            Assert.False(string.IsNullOrEmpty(question.YesNegative));
            Assert.False(string.IsNullOrEmpty(question.NoPositive));
            Assert.False(string.IsNullOrEmpty(question.NoNegative));
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
