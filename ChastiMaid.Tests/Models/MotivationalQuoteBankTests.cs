using ChastiMaid.Models;

namespace ChastiMaid.Tests.Models;

public class MotivationalQuoteBankTests
{
    [Fact]
    public void GetRandom_AlwaysReturnsNonEmptyKey()
    {
        for (var i = 0; i < 100; i++)
        {
            var key = MotivationalQuoteBank.GetRandom();

            Assert.False(string.IsNullOrEmpty(key));
        }
    }

    [Fact]
    public void GetRandom_WithSameSeed_IsDeterministic()
    {
        var first = MotivationalQuoteBank.GetRandom(new Random(42));
        var second = MotivationalQuoteBank.GetRandom(new Random(42));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetRandom_OverManyIterations_ProducesVariety()
    {
        var keys = Enumerable.Range(0, 50)
            .Select(seed => MotivationalQuoteBank.GetRandom(new Random(seed)))
            .Distinct()
            .ToList();

        Assert.True(keys.Count > 1);
    }
}
