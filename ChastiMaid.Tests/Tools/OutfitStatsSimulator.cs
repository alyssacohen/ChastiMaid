using ChastiMaid.Models;
using ChastiMaid.Services;

namespace ChastiMaid.Tests.Tools;

public record ItemStat(
    int Id,
    string Name,
    int TimesSelected,
    double SelectionRate,
    double InitialWeight,
    double FinalWeight,
    int FinalRejectionCount,
    int TimesForced = 0);

public record SimulationResult(
    string ScenarioName,
    int Iterations,
    int AcceptedCount,
    double AverageItemsPerOutfit,
    double AverageMultiplier,
    List<ItemStat> ItemStats);

public record ForcedSimulationResult(
    string ScenarioName,
    int Iterations,
    int AcceptedCount,
    int ForcedSessionCount,
    double AverageItemsPerOutfit,
    double AverageMultiplier,
    List<ItemStat> ItemStats);

/// <summary>
///     Repeatedly builds outfits with <see cref="OutfitBuilder" /> and applies the same accept/reject weight
///     mutation as <c>SaveRepository.AcceptItems</c>/<c>RejectItems</c> — including the periodic rescale that
///     restores the wardrobe's total Weight every <see cref="WardrobeBalance.RescaleEveryNRejections" /> rejected
///     outfits — in memory, to observe how Weight drifts and how that drift shapes future selection odds across
///     consecutive sessions.
/// </summary>
public static class OutfitStatsSimulator
{
    /// <summary>Mirrors <c>SaveRepository.RescaleWardrobeWeights</c>: rescales every item's Weight so the wardrobe total returns to <paramref name="initialTotal" />.</summary>
    private static void RescaleWeights(List<ClothingItem> items, double initialTotal)
    {
        var currentTotal = items.Sum(i => i.Weight);
        if (currentTotal <= 0)
        {
            return;
        }

        var factor = initialTotal / currentTotal;
        foreach (var item in items)
        {
            item.Weight *= factor;
        }
    }

    public static SimulationResult Run(
        List<ClothingItem> items,
        List<OutfitRule> rules,
        int iterations,
        Func<int, Outfit, bool> shouldAccept,
        int masterSeed = 12345,
        string scenarioName = "scenario")
    {
        var initialWeights = items.ToDictionary(i => i.Id, i => i.Weight);
        var initialTotal = items.Sum(i => i.Weight);
        var selectionCounts = items.ToDictionary(i => i.Id, _ => 0);
        var masterRandom = new Random(masterSeed);

        var totalItemsPlaced = 0;
        var totalMultiplier = 0.0;
        var acceptedCount = 0;
        var rejectionsSinceRescale = 0;

        for (var session = 0; session < iterations; session++)
        {
            var builder = new OutfitBuilder(items, rules, masterRandom.Next());
            var outfit = builder.Build();

            foreach (var item in outfit.Items)
            {
                selectionCounts[item.Id]++;
            }

            totalItemsPlaced += outfit.Items.Count;
            totalMultiplier += outfit.TotalMultiplier;

            if (shouldAccept(session, outfit))
            {
                acceptedCount++;
                foreach (var item in outfit.Items)
                {
                    item.Weight = Math.Max(
                        WardrobeBalance.AcceptWeightFloor,
                        item.Weight - WardrobeBalance.AcceptWeightDecrement);
                    if (item.RejectionCount > 0)
                    {
                        item.RejectionCount--;
                    }
                }
            }
            else
            {
                foreach (var item in outfit.Items)
                {
                    item.Weight += WardrobeBalance.RejectWeightIncrement;
                    item.RejectionCount++;
                }

                rejectionsSinceRescale++;
                if (rejectionsSinceRescale >= WardrobeBalance.RescaleEveryNRejections)
                {
                    RescaleWeights(items, initialTotal);
                    rejectionsSinceRescale = 0;
                }
            }
        }

        var itemStats = items
            .Select(i => new ItemStat(
                i.Id,
                i.Name,
                selectionCounts[i.Id],
                iterations == 0 ? 0 : (double)selectionCounts[i.Id] / iterations,
                initialWeights[i.Id],
                i.Weight,
                i.RejectionCount))
            .OrderByDescending(s => s.SelectionRate)
            .ToList();

        return new SimulationResult(
            scenarioName,
            iterations,
            acceptedCount,
            iterations == 0 ? 0 : (double)totalItemsPlaced / iterations,
            iterations == 0 ? 0 : totalMultiplier / iterations,
            itemStats);
    }

    /// <summary>Convenience overload for scenarios that decide accept/reject purely from the session index.</summary>
    public static SimulationResult Run(
        List<ClothingItem> items,
        List<OutfitRule> rules,
        int iterations,
        Func<int, bool> shouldAccept,
        int masterSeed = 12345,
        string scenarioName = "scenario")
    {
        return Run(items, rules, iterations, (session, _) => shouldAccept(session), masterSeed, scenarioName);
    }

    /// <summary>
    ///     Same loop as <see cref="Run(System.Collections.Generic.List{ChastiMaid.Models.ClothingItem},System.Collections.Generic.List{ChastiMaid.Models.OutfitRule},int,System.Func{int,ChastiMaid.Models.Outfit,bool},int,string)" />
    ///     but mirrors <c>StartSessionViewModel</c>'s "Priority 2" rule: before building each outfit, any item with
    ///     <c>RejectionCount &gt;= rejectionThreshold</c> is force-seeded into the outfit and the session is locked to
    ///     Accept-only (regenerate/modify are disabled in the real UI when <c>IsForced</c> is true) — <paramref name="shouldAccept" />
    ///     is not consulted for forced sessions.
    /// </summary>
    public static ForcedSimulationResult RunWithForcedThreshold(
        List<ClothingItem> items,
        List<OutfitRule> rules,
        int iterations,
        Func<int, Outfit, bool> shouldAccept,
        int rejectionThreshold = WardrobeBalance.RejectionForceThreshold,
        int masterSeed = 12345,
        string scenarioName = "scenario")
    {
        var initialWeights = items.ToDictionary(i => i.Id, i => i.Weight);
        var initialTotal = items.Sum(i => i.Weight);
        var selectionCounts = items.ToDictionary(i => i.Id, _ => 0);
        var forcedCounts = items.ToDictionary(i => i.Id, _ => 0);
        var masterRandom = new Random(masterSeed);

        var totalItemsPlaced = 0;
        var totalMultiplier = 0.0;
        var acceptedCount = 0;
        var forcedSessionCount = 0;
        var rejectionsSinceRescale = 0;

        for (var session = 0; session < iterations; session++)
        {
            // Mirrors StartSessionViewModel.cs:103 — evaluated against the wardrobe *before* this outfit is built.
            var overThreshold = items.Where(i => i.RejectionCount >= rejectionThreshold).ToList();
            var isForced = overThreshold.Count > 0;

            var builder = new OutfitBuilder(items, rules, masterRandom.Next());
            if (isForced)
            {
                builder.AddSeedItems(overThreshold);
            }

            var outfit = builder.Build();

            foreach (var item in outfit.Items)
            {
                selectionCounts[item.Id]++;
            }

            totalItemsPlaced += outfit.Items.Count;
            totalMultiplier += outfit.TotalMultiplier;

            bool accept;
            if (isForced)
            {
                forcedSessionCount++;
                foreach (var item in overThreshold)
                {
                    forcedCounts[item.Id]++;
                }

                accept = true; // forced sessions only expose AcceptCommand
            }
            else
            {
                accept = shouldAccept(session, outfit);
            }

            if (accept)
            {
                acceptedCount++;
                foreach (var item in outfit.Items)
                {
                    item.Weight = Math.Max(
                        WardrobeBalance.AcceptWeightFloor,
                        item.Weight - WardrobeBalance.AcceptWeightDecrement);
                    if (item.RejectionCount > 0)
                    {
                        item.RejectionCount--;
                    }
                }
            }
            else
            {
                foreach (var item in outfit.Items)
                {
                    item.Weight += WardrobeBalance.RejectWeightIncrement;
                    item.RejectionCount++;
                }

                rejectionsSinceRescale++;
                if (rejectionsSinceRescale >= WardrobeBalance.RescaleEveryNRejections)
                {
                    RescaleWeights(items, initialTotal);
                    rejectionsSinceRescale = 0;
                }
            }
        }

        var itemStats = items
            .Select(i => new ItemStat(
                i.Id,
                i.Name,
                selectionCounts[i.Id],
                iterations == 0 ? 0 : (double)selectionCounts[i.Id] / iterations,
                initialWeights[i.Id],
                i.Weight,
                i.RejectionCount,
                forcedCounts[i.Id]))
            .OrderByDescending(s => s.SelectionRate)
            .ToList();

        return new ForcedSimulationResult(
            scenarioName,
            iterations,
            acceptedCount,
            forcedSessionCount,
            iterations == 0 ? 0 : (double)totalItemsPlaced / iterations,
            iterations == 0 ? 0 : totalMultiplier / iterations,
            itemStats);
    }

    /// <summary>Convenience overload for scenarios that decide accept/reject purely from the session index.</summary>
    public static ForcedSimulationResult RunWithForcedThreshold(
        List<ClothingItem> items,
        List<OutfitRule> rules,
        int iterations,
        Func<int, bool> shouldAccept,
        int rejectionThreshold = WardrobeBalance.RejectionForceThreshold,
        int masterSeed = 12345,
        string scenarioName = "scenario")
    {
        return RunWithForcedThreshold(
            items, rules, iterations, (session, _) => shouldAccept(session), rejectionThreshold, masterSeed,
            scenarioName);
    }

    public static void Print(SimulationResult result, Action<string> writeLine)
    {
        writeLine($"=== {result.ScenarioName} ({result.Iterations} outfits, {result.AcceptedCount} accepted) ===");
        writeLine($"Avg items/outfit: {result.AverageItemsPerOutfit:F2}   Avg multiplier: {result.AverageMultiplier:F3}");
        writeLine($"{"Item",-26}{"Picked",8}{"Pick%",8}{"W0",8}{"Wf",8}{"ΔW",8}{"Reject",8}");
        foreach (var s in result.ItemStats)
        {
            writeLine(
                $"{s.Name,-26}{s.TimesSelected,8}{s.SelectionRate * 100,7:F1}%{s.InitialWeight,8:F2}{s.FinalWeight,8:F2}{s.FinalWeight - s.InitialWeight,8:F2}{s.FinalRejectionCount,8}");
        }

        writeLine("");
    }

    public static void Print(ForcedSimulationResult result, Action<string> writeLine)
    {
        writeLine(
            $"=== {result.ScenarioName} ({result.Iterations} outfits, {result.AcceptedCount} accepted, {result.ForcedSessionCount} forced) ===");
        writeLine($"Avg items/outfit: {result.AverageItemsPerOutfit:F2}   Avg multiplier: {result.AverageMultiplier:F3}");
        writeLine($"{"Item",-26}{"Picked",8}{"Pick%",8}{"W0",8}{"Wf",8}{"ΔW",8}{"Reject",8}{"Forced",8}");
        foreach (var s in result.ItemStats)
        {
            writeLine(
                $"{s.Name,-26}{s.TimesSelected,8}{s.SelectionRate * 100,7:F1}%{s.InitialWeight,8:F2}{s.FinalWeight,8:F2}{s.FinalWeight - s.InitialWeight,8:F2}{s.FinalRejectionCount,8}{s.TimesForced,8}");
        }

        writeLine("");
    }
}
