using ChastiMaid.Models;
using ChastiMaid.Repositories;

namespace ChastiMaid.Services;

public enum FortuneOutcomeKind
{
    Collectible,
    CashWin,
    CashLoss,
    SuspendedProbability,
    SuspendedDuration,
    SuspendedGrant,
    SoftenRandom,
    CancelRandom,
    AggravateRandom,
    Whiff,
}

/// <summary>The already-resolved outcome of a single draw. Value carries the sign for cash/probability/duration effects.</summary>
public record FortuneDrawResult(FortuneOutcomeKind Kind, CardId? Card, int Value, PermissionKind? Target, int? PunishmentId);

/// <summary>
///     Weighted-pool entry: either a collectible card copy (weight 10, one entry per currently available copy) or a
///     fixed immediate effect. See docs/specs/fortune-deck.md "Card Pool" for the weight table this mirrors.
/// </summary>
internal record FortunePoolEntry(int Weight, Func<Random, SaveRepository, FortuneDrawResult> Resolve);

/// <summary>
///     The Fortune Deck ("Il Ventaglio della Sorte") — a paid single-card draw from a simulated physical deck, shared
///     with the Shop's collectible card stock. See docs/specs/fortune-deck.md for the full model.
/// </summary>
public class FortuneDeck
{
    private readonly Random _random;
    private readonly SaveRepository _saveRepo;
    private readonly GameSettings _settings;

    public FortuneDeck(SaveRepository saveRepo, GameSettings settings, Random? random = null)
    {
        _saveRepo = saveRepo;
        _settings = settings;
        _random = random ?? Random.Shared;
    }

    /// <summary>Charges the draw stake, builds the current weighted pool, picks and resolves one entry.</summary>
    public FortuneDrawResult Draw()
    {
        _saveRepo.ChargeExpense(CardCatalog.FortuneDrawPrice(_settings));

        var pool = BuildPool();
        var totalWeight = pool.Sum(e => e.Weight);
        var roll = _random.Next(totalWeight);
        var cumulative = 0;
        foreach (var entry in pool)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
            {
                return entry.Resolve(_random, _saveRepo);
            }
        }

        // Unreachable in practice (roll < totalWeight always matches), but keeps the method total.
        return new FortuneDrawResult(FortuneOutcomeKind.Whiff, null, 0, null, null);
    }

    private List<FortunePoolEntry> BuildPool()
    {
        var pool = new List<FortunePoolEntry>();

        // Collectible cards: one weight-10 entry per currently available copy. CooldownBypass has no
        // immediate/suspended variant (per fortune-deck.md) but remains a collectible-only draw.
        foreach (var def in CardCatalog.All)
        {
            var available = _saveRepo.AvailableCopies(def.Id);
            for (var i = 0; i < available; i++)
            {
                var id = def.Id;
                pool.Add(new FortunePoolEntry(10, (_, repo) =>
                {
                    repo.GrantCard(id);
                    return new FortuneDrawResult(FortuneOutcomeKind.Collectible, id, 0, null, null);
                }));
            }
        }

        pool.AddRange(ImmediateEffectEntries());
        return pool;
    }

    private IEnumerable<FortunePoolEntry> ImmediateEffectEntries()
    {
        // --- Immediate positive effects (286 total) ---
        yield return CashEntry(45, 2);
        yield return CashEntry(18, 5);
        yield return CashEntry(9, 10);

        yield return PermissionEntry(41, FortuneOutcomeKind.SuspendedProbability, SuspendedEffect.Probability, 5);
        yield return PermissionEntry(20, FortuneOutcomeKind.SuspendedProbability, SuspendedEffect.Probability, 10);
        yield return PermissionEntry(10, FortuneOutcomeKind.SuspendedProbability, SuspendedEffect.Probability, 20);

        yield return PermissionEntry(47, FortuneOutcomeKind.SuspendedDuration, SuspendedEffect.Duration, 1);
        yield return PermissionEntry(15, FortuneOutcomeKind.SuspendedDuration, SuspendedEffect.Duration, 3);
        yield return PermissionEntry(9, FortuneOutcomeKind.SuspendedDuration, SuspendedEffect.Duration, 5);

        yield return PunishmentEntry(38, FortuneOutcomeKind.SoftenRandom, repo => repo.SoftenPunishment);
        yield return PermissionEntry(18, FortuneOutcomeKind.SuspendedGrant, SuspendedEffect.Grant, 0);
        yield return PunishmentEntry(16, FortuneOutcomeKind.CancelRandom, repo => repo.CancelPunishment);

        // --- Immediate negative effects (715 total) ---
        yield return new FortunePoolEntry(215, (_, _) => Whiff());

        yield return PunishmentEntry(107, FortuneOutcomeKind.AggravateRandom, repo => repo.AggravatePunishment);

        yield return CashEntry(87, -1);
        yield return CashEntry(44, -2);

        yield return PermissionEntry(75, FortuneOutcomeKind.SuspendedProbability, SuspendedEffect.Probability, -5);
        yield return PermissionEntry(37, FortuneOutcomeKind.SuspendedProbability, SuspendedEffect.Probability, -10);
        yield return PermissionEntry(19, FortuneOutcomeKind.SuspendedProbability, SuspendedEffect.Probability, -20);

        yield return PermissionEntry(86, FortuneOutcomeKind.SuspendedDuration, SuspendedEffect.Duration, -1);
        yield return PermissionEntry(28, FortuneOutcomeKind.SuspendedDuration, SuspendedEffect.Duration, -3);
        yield return PermissionEntry(17, FortuneOutcomeKind.SuspendedDuration, SuspendedEffect.Duration, -5);
    }

    private FortunePoolEntry CashEntry(int weight, int stakeMultiplier)
    {
        return new FortunePoolEntry(weight, (_, repo) =>
        {
            var amount = Math.Abs(stakeMultiplier) * CardCatalog.FortuneDrawPrice(_settings);
            if (stakeMultiplier > 0)
            {
                repo.CreditEarnings(amount); // a win credits like earnings — never touches InitialDebt
            }
            else
            {
                repo.ChargeExpense(amount); // a loss grows the amount owed, like a fine
            }

            return new FortuneDrawResult(
                stakeMultiplier > 0 ? FortuneOutcomeKind.CashWin : FortuneOutcomeKind.CashLoss,
                null, stakeMultiplier, null, null);
        });
    }

    private FortunePoolEntry PermissionEntry(int weight, FortuneOutcomeKind kind, SuspendedEffect effect, int value)
    {
        return new FortunePoolEntry(weight, (random, repo) =>
        {
            var kinds = Enum.GetValues<PermissionKind>();
            var target = kinds[random.Next(kinds.Length)];
            repo.AddSuspendedModifier(target, effect, value);
            return new FortuneDrawResult(kind, null, value, target, null);
        });
    }

    private FortunePoolEntry PunishmentEntry(int weight, FortuneOutcomeKind kind, Func<SaveRepository, Func<int, bool>> apply)
    {
        return new FortunePoolEntry(weight, (random, repo) =>
        {
            var eligible = repo.GetActivePunishments();
            if (eligible.Count == 0)
            {
                return Whiff();
            }

            var target = eligible[random.Next(eligible.Count)];
            apply(repo)(target.Id);
            return new FortuneDrawResult(kind, null, 0, null, target.Id);
        });
    }

    private static FortuneDrawResult Whiff()
    {
        return new FortuneDrawResult(FortuneOutcomeKind.Whiff, null, 0, null, null);
    }
}
