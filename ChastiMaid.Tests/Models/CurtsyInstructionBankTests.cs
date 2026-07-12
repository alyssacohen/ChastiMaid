using ChastiMaid.Models;

namespace ChastiMaid.Tests.Models;

public class CurtsyInstructionBankTests
{
    [Fact]
    public void GetRandom_AlwaysReturnsNonEmptyKey()
    {
        for (var i = 0; i < 100; i++)
        {
            var key = CurtsyInstructionBank.GetRandom();

            Assert.False(string.IsNullOrEmpty(key));
        }
    }

    [Fact]
    public void GetRandom_WithSameSeed_IsDeterministic()
    {
        var first = CurtsyInstructionBank.GetRandom(new Random(42));
        var second = CurtsyInstructionBank.GetRandom(new Random(42));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetRandom_OverManyIterations_ProducesVariety()
    {
        var keys = Enumerable.Range(0, 50)
            .Select(seed => CurtsyInstructionBank.GetRandom(new Random(seed)))
            .Distinct()
            .ToList();

        Assert.True(keys.Count > 1);
    }
}
