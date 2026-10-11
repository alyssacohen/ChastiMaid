using ChastiMaid.Models;

namespace ChastiMaid.Services;

/// <summary>
///     Single source of truth for the variable-severity mechanic (contract clause "Variable Severity"): the
///     employer's displeasure ("mood", 0 = content, 1 = thoroughly displeased) and the coefficient drawn from it
///     that multiplies an infraction's severity when a punishment is assigned. Fixed constants, deliberately not
///     settings — Madame does not disclose her methods. See docs/specs/punishments.md "Variable severity".
/// </summary>
public static class SeverityBalance
{
    /// <summary>Coefficient reached when every factor is saturated and the draw lands on its ceiling.</summary>
    public const double MaxCoefficient = 3.0;

    // Share of the mood each factor can contribute. They add up to 1.
    public const double ChoreWeight = 0.4;
    public const double RefusalWeight = 0.3;
    public const double PendingWeight = 0.2;
    public const double DebtWeight = 0.1;

    /// <summary>Explicitly refused garments (GameState.OutfitRefusals) at which the wardrobe factor is at its maximum.</summary>
    public const int RefusalSaturation = 20;

    /// <summary>Pending punishments at which the pending factor is at its maximum.</summary>
    public const int PendingSaturation = 10;

    /// <summary>Remaining share of the debt at or below which the debt factor is zero.</summary>
    public const double DebtFloor = 1.0 / 3.0;

    /// <summary>The date a chore's recommended frequency counts from: its last completion, never earlier than the
    /// start of the game — a chore never done is not overdue on day one.</summary>
    public static DateTime ChoreReferenceDate(Chore chore, DateTime gameStartedAt)
    {
        return chore.LastCompletedAt is { } completed && completed > gameStartedAt ? completed : gameStartedAt;
    }

    /// <summary>0 while the chore is within its recommended frequency, rising to 1 at twice that frequency.</summary>
    public static double ChoreLateness(Chore chore, DateTime gameStartedAt, DateTime now)
    {
        var frequency = Math.Max(1, chore.FrequencyDays);
        var days = (now - ChoreReferenceDate(chore, gameStartedAt)).TotalDays;
        return Math.Clamp((days - frequency) / frequency, 0, 1);
    }

    /// <summary>Mean lateness over the recurring chores (occasional tasks have no cadence). 0 when there are none.</summary>
    public static double AverageChoreLateness(IEnumerable<Chore> chores, DateTime gameStartedAt, DateTime now)
    {
        return chores
            .Where(c => !c.IsOccasional)
            .Select(c => ChoreLateness(c, gameStartedAt, now))
            .DefaultIfEmpty(0)
            .Average();
    }

    public static double Mood(double choreLateness, int refusals, int pending, decimal currentDebt, decimal initialDebt)
    {
        // Interest can push the remaining share above 1 and overpayment below 0.
        var debtShare = initialDebt > 0 ? Math.Clamp((double)(currentDebt / initialDebt), 0, 1) : 0;

        return ChoreWeight * Math.Clamp(choreLateness, 0, 1)
            + RefusalWeight * Math.Clamp((double)refusals / RefusalSaturation, 0, 1)
            + PendingWeight * Math.Clamp((double)pending / PendingSaturation, 0, 1)
            + DebtWeight * Math.Clamp((debtShare - DebtFloor) / (1 - DebtFloor), 0, 1);
    }

    /// <summary>Draws the coefficient between halfway and the mood's ceiling: bad conduct is always felt, by an
    /// amount that stays unpredictable. Exactly 1 when the mood is 0.</summary>
    public static double Coefficient(double mood, Random rng)
    {
        return 1 + mood * (MaxCoefficient - 1) * (0.5 + 0.5 * rng.NextDouble());
    }
}
