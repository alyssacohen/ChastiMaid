using ChastiMaid.Models;

namespace ChastiMaid.Tests.Models;

public class EmployerPhraseBankTests
{
    [Fact]
    public void GetRandom_AlwaysReturnsNonEmptyKey()
    {
        for (var i = 0; i < 100; i++)
        {
            var key = EmployerPhraseBank.GetRandom();

            Assert.False(string.IsNullOrEmpty(key));
        }
    }

    [Fact]
    public void GetRandom_WithSameSeed_IsDeterministic()
    {
        var first = EmployerPhraseBank.GetRandom(new Random(42));
        var second = EmployerPhraseBank.GetRandom(new Random(42));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetRandom_OverManyIterations_ProducesVariety()
    {
        var keys = Enumerable.Range(0, 50)
            .Select(seed => EmployerPhraseBank.GetRandom(new Random(seed)))
            .Distinct()
            .ToList();

        Assert.True(keys.Count > 1);
    }

    [Fact]
    public void AllKeys_HasNoDuplicatesOrEmptyEntries()
    {
        Assert.Equal(EmployerPhraseBank.All.Count, EmployerPhraseBank.All.Distinct().Count());
        Assert.All(EmployerPhraseBank.All, key => Assert.False(string.IsNullOrEmpty(key)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void GetRandomSet_ReturnsRequestedCountOfDistinctKeys(int count)
    {
        var set = EmployerPhraseBank.GetRandomSet(count);

        Assert.Equal(count, set.Count);
        Assert.Equal(count, set.Distinct().Count());
        Assert.All(set, key => Assert.Contains(key, EmployerPhraseBank.All));
    }
}
