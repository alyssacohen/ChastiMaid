using ChastiMaid.Models;
using ChastiMaid.Services;

namespace ChastiMaid.Tests.Services;

public class SeverityBalanceTests
{
    private static readonly DateTime GameStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // The scenario table of docs/specs/punishments.md "Variable severity": keep the two in step.
    [Theory]
    [InlineData(0.0, 0, 0, 1000, 1.20)] // game start, perfect conduct: only the full debt weighs
    [InlineData(0.2, 0, 2, 600, 1.32)] // mid-game: 1 chore in 5 at double its frequency, 2 pending, 60% owed
    [InlineData(0.8, 0, 0, 800, 1.78)] // back after a week away
    [InlineData(1.0, 20, 10, 1000, 3.00)] // worst case
    [InlineData(0.0, 0, 0, 300, 1.00)] // endgame, nothing to reproach
    public void Mood_DocumentedScenarios_GiveTheDocumentedCeiling(
        double choreLateness, int refusals, int pending, int currentDebt, double expectedCeiling)
    {
        var mood = SeverityBalance.Mood(choreLateness, refusals, pending, currentDebt, 1000);

        Assert.Equal(expectedCeiling, 1 + mood * (SeverityBalance.MaxCoefficient - 1), 2);
    }

    [Fact]
    public void Coefficient_IsExactlyOneWhenThereIsNothingToReproach()
    {
        var mood = SeverityBalance.Mood(0, 0, 0, 300, 1000);

        Assert.Equal(1.0, SeverityBalance.Coefficient(mood, new Random(1)));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Coefficient_IsDrawnBetweenHalfwayAndTheMoodCeiling(double mood)
    {
        var rng = new Random(42);
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(SeverityBalance.Coefficient(mood, rng), 1 + mood, 1 + 2 * mood);
        }
    }

    [Fact]
    public void Mood_EachFactorSaturatesAndTheDebtShareIsClamped()
    {
        Assert.Equal(SeverityBalance.Mood(1, 0, 0, 0, 1000), SeverityBalance.Mood(5, 0, 0, 0, 1000));
        Assert.Equal(SeverityBalance.Mood(0, 20, 0, 0, 1000), SeverityBalance.Mood(0, 500, 0, 0, 1000));
        Assert.Equal(SeverityBalance.Mood(0, 0, 10, 0, 1000), SeverityBalance.Mood(0, 0, 99, 0, 1000));
        Assert.Equal(SeverityBalance.Mood(0, 0, 0, 1000, 1000), SeverityBalance.Mood(0, 0, 0, 5000, 1000)); // interest
        Assert.Equal(0, SeverityBalance.Mood(0, 0, 0, -200, 1000)); // credit
        Assert.Equal(0, SeverityBalance.Mood(0, 0, 0, 100, 0)); // no debt total to compare against
    }

    [Fact]
    public void ChoreLateness_CountsFromTheStartOfTheGameAndPeaksAtTwiceTheFrequency()
    {
        var neverDone = new Chore { FrequencyDays = 2 };
        var staleSnapshotDate = new Chore { FrequencyDays = 2, LastCompletedAt = GameStart.AddDays(-30) };
        var doneOnDayThree = new Chore { FrequencyDays = 2, LastCompletedAt = GameStart.AddDays(3) };

        Assert.Equal(0, SeverityBalance.ChoreLateness(neverDone, GameStart, GameStart));
        Assert.Equal(0, SeverityBalance.ChoreLateness(neverDone, GameStart, GameStart.AddDays(2)));
        Assert.Equal(0.5, SeverityBalance.ChoreLateness(neverDone, GameStart, GameStart.AddDays(3)), 6);
        Assert.Equal(1, SeverityBalance.ChoreLateness(neverDone, GameStart, GameStart.AddDays(40)));
        Assert.Equal(0, SeverityBalance.ChoreLateness(staleSnapshotDate, GameStart, GameStart.AddDays(1)));
        Assert.Equal(0, SeverityBalance.ChoreLateness(doneOnDayThree, GameStart, GameStart.AddDays(5)));
    }

    [Fact]
    public void AverageChoreLateness_IgnoresOccasionalTasksAndIsZeroWithoutChores()
    {
        var now = GameStart.AddDays(40);
        List<Chore> chores =
        [
            new() { FrequencyDays = 1 },
            new() { FrequencyDays = 365 },
            new() { FrequencyDays = 1, IsOccasional = true },
        ];

        Assert.Equal(0.5, SeverityBalance.AverageChoreLateness(chores, GameStart, now), 6);
        Assert.Equal(0, SeverityBalance.AverageChoreLateness([], GameStart, now)); // never NaN
    }
}
