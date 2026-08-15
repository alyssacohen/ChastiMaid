using Xunit.Abstractions;

namespace ChastiMaid.Tests.Tools;

/// <summary>
///     Statistical harness for the outfit generator. Run via:
///     dotnet test --filter "FullyQualifiedName~OutfitStatsSimulatorTests" -l "console;verbosity=detailed"
///     Each [Theory] case is an accept/reject scenario; add InlineData rows to explore others.
/// </summary>
public class OutfitStatsSimulatorTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Accept all", 100, 1, 1)]
    [InlineData("Reject all", 100, 0, 1)]
    [InlineData("Reject 1 in 5", 100, 4, 5)]
    [InlineData("Reject 1 in 2", 100, 1, 2)]
    [InlineData("Reject 4 in 5", 100, 1, 5)]
    public void Simulate_FixedAcceptRejectPattern(string name, int iterations, int acceptOutOfEvery, int every)
    {
        var (items, rules) = WardrobeFixture.CreateSeedWardrobe();
        var result = OutfitStatsSimulator.Run(
            items,
            rules,
            iterations,
            session => session % every < acceptOutOfEvery,
            scenarioName: name);

        OutfitStatsSimulator.Print(result, output.WriteLine);
    }

    [Theory]
    [InlineData("Random accept 80%", 100, 0.8, 1)]
    [InlineData("Random accept 50%", 100, 0.5, 1)]
    [InlineData("Random accept 20%", 100, 0.2, 1)]
    public void Simulate_RandomAcceptProbability(string name, int iterations, double acceptProbability, int decisionSeed)
    {
        var (items, rules) = WardrobeFixture.CreateSeedWardrobe();
        var decisionRandom = new Random(decisionSeed);
        var result = OutfitStatsSimulator.Run(
            items,
            rules,
            iterations,
            _ => decisionRandom.NextDouble() < acceptProbability,
            scenarioName: name);

        OutfitStatsSimulator.Print(result, output.WriteLine);
    }

    [Fact]
    public void Simulate_RejectDislikedTags_BiasesWeightAwayFromThem()
    {
        var (items, rules) = WardrobeFixture.CreateSeedWardrobe();

        // Scenario: reject every outfit containing "latex" or "fetish"-flavoured pieces,
        // accept everything else — observe whether their Weight climbs and selection rate falls.
        var dislikedIds = items
            .Where(i => i.Name.Contains("latex") || i.Name.Contains("diaper"))
            .Select(i => i.Id)
            .ToHashSet();

        var result = OutfitStatsSimulator.Run(
            items,
            rules,
            100,
            (_, outfit) => !outfit.Items.Any(i => dislikedIds.Contains(i.Id)),
            scenarioName: "Reject latex/diaper outfits");

        OutfitStatsSimulator.Print(result, output.WriteLine);
    }

    [Fact]
    public void Simulate_RejectDislikedTags_WithForcedThreshold_EventuallyTamesWeight()
    {
        var (items, rules) = WardrobeFixture.CreateSeedWardrobe();

        // Same scenario as Simulate_RejectDislikedTags_BiasesWeightAwayFromThem, but this time the real
        // "RejectionCount >= 5" forced-seed rule from StartSessionViewModel is also simulated: once a disliked
        // item is rejected 5 times, it gets force-seeded into a non-editable, accept-only session.
        var dislikedIds = items
            .Where(i => i.Name.Contains("latex") || i.Name.Contains("diaper"))
            .Select(i => i.Id)
            .ToHashSet();

        var result = OutfitStatsSimulator.RunWithForcedThreshold(
            items,
            rules,
            100,
            (_, outfit) => !outfit.Items.Any(i => dislikedIds.Contains(i.Id)),
            scenarioName: "Reject latex/diaper outfits, with forced threshold");

        OutfitStatsSimulator.Print(result, output.WriteLine);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(3)]
    [InlineData(8)]
    public void Simulate_RejectDislikedTags_VaryingThreshold(int rejectionThreshold)
    {
        var (items, rules) = WardrobeFixture.CreateSeedWardrobe();

        var dislikedIds = items
            .Where(i => i.Name.Contains("latex") || i.Name.Contains("diaper"))
            .Select(i => i.Id)
            .ToHashSet();

        var result = OutfitStatsSimulator.RunWithForcedThreshold(
            items,
            rules,
            100,
            (_, outfit) => !outfit.Items.Any(i => dislikedIds.Contains(i.Id)),
            rejectionThreshold: rejectionThreshold,
            scenarioName: $"Reject latex/diaper, threshold={rejectionThreshold}");

        OutfitStatsSimulator.Print(result, output.WriteLine);
    }
}
