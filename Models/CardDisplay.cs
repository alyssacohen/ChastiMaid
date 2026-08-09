using ChastiMaid.Localization;

namespace ChastiMaid.Models;

/// <summary>Localized name/description for a catalogue card, shared by the Shop and Fortune Deck screens.</summary>
public static class CardDisplay
{
    public static string NameOf(CardId id)
    {
        return id.Type switch
        {
            CardType.CooldownBypass => Resources.Get("CardCooldownBypassName"),
            CardType.DurationBoost => string.Format(Resources.Get("CardDurationBoostName"), id.Variant),
            CardType.ProbabilityBoost => string.Format(Resources.Get("CardProbabilityBoostName"), id.Variant),
            CardType.GrantPermission => Resources.Get("CardGrantPermissionName"),
            CardType.SoftenPunishment => Resources.Get("CardSoftenPunishmentName"),
            CardType.CancelPunishment => Resources.Get("CardCancelPunishmentName"),
            _ => id.Type.ToString(),
        };
    }

    public static string DescriptionOf(CardId id)
    {
        return id.Type switch
        {
            CardType.CooldownBypass => Resources.Get("CardCooldownBypassDesc"),
            CardType.DurationBoost => Resources.Get("CardDurationBoostDesc"),
            CardType.ProbabilityBoost => Resources.Get("CardProbabilityBoostDesc"),
            CardType.GrantPermission => Resources.Get("CardGrantPermissionDesc"),
            CardType.SoftenPunishment => Resources.Get("CardSoftenPunishmentDesc"),
            CardType.CancelPunishment => Resources.Get("CardCancelPunishmentDesc"),
            _ => "",
        };
    }
}
