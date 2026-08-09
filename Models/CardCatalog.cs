namespace ChastiMaid.Models;

public enum CardType
{
    CooldownBypass,
    DurationBoost,
    ProbabilityBoost,
    GrantPermission,
    SoftenPunishment,
    CancelPunishment,
}

/// <summary>
///     Identifies a card in the catalogue. <paramref name="Variant" /> is the added minutes for
///     <see cref="CardType.DurationBoost" /> and the added percentage points for <see cref="CardType.ProbabilityBoost" />;
///     it is 0 for the single-tier cards.
/// </summary>
public readonly record struct CardId(CardType Type, int Variant);

/// <summary>
///     A catalogue entry. <paramref name="PriceMultiplier" /> is multiplied by the configured idle wage to get the
///     price; <paramref name="BaseCopies" /> is how many physical copies of this card exist in the shared pool.
/// </summary>
public record CardDefinition(CardId Id, int PriceMultiplier, int BaseCopies);

/// <summary>
///     Fixed card catalogue shared by the Shop and the Fortune Deck. Prices and copy counts are constants, not
///     settings — see docs/specs/shop.md and docs/specs/fortune-deck.md.
/// </summary>
public static class CardCatalog
{
    public static readonly IReadOnlyList<CardDefinition> All =
    [
        new(new CardId(CardType.CooldownBypass, 0), 10, 6),
        new(new CardId(CardType.DurationBoost, 1), 10, 6),
        new(new CardId(CardType.DurationBoost, 3), 30, 5),
        new(new CardId(CardType.DurationBoost, 5), 50, 4),
        new(new CardId(CardType.ProbabilityBoost, 5), 5, 6),
        new(new CardId(CardType.ProbabilityBoost, 10), 10, 5),
        new(new CardId(CardType.ProbabilityBoost, 20), 20, 4),
        new(new CardId(CardType.GrantPermission, 0), 110, 2),
        new(new CardId(CardType.SoftenPunishment, 0), 50, 3),
        new(new CardId(CardType.CancelPunishment, 0), 120, 2),
    ];

    /// <summary>Cost of a single Fortune Deck draw, as a multiplier of the idle wage.</summary>
    public const int FortuneDrawPriceMultiplier = 10;

    public static CardDefinition? Find(CardId id)
    {
        return All.FirstOrDefault(d => d.Id == id);
    }

    public static int BaseCopies(CardId id)
    {
        return Find(id)?.BaseCopies ?? 0;
    }

    public static decimal PriceOf(CardId id, GameSettings settings)
    {
        return Math.Round((Find(id)?.PriceMultiplier ?? 0) * settings.IdleWageAmount, 0);
    }

    public static decimal FortuneDrawPrice(GameSettings settings)
    {
        return Math.Round(FortuneDrawPriceMultiplier * settings.IdleWageAmount, 0);
    }

    /// <summary>True for the cards played from the permission-request menu.</summary>
    public static bool IsPermissionCard(CardType type)
    {
        return type is CardType.CooldownBypass or CardType.DurationBoost or CardType.ProbabilityBoost
            or CardType.GrantPermission;
    }
}
