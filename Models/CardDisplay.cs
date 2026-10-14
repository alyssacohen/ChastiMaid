using ChastiMaid.Localization;

namespace ChastiMaid.Models;

/// <summary>Localized name/description for a catalogue card, shared by the Shop and Fortune Deck screens.</summary>
public static class CardDisplay
{
    public static string NameOf(CardId id, ILocalizer L)
    {
        return id.Type switch
        {
            CardType.CooldownBypass => L.T("Eager"),
            CardType.DurationBoost => L.T("+{0} Minutes", id.Variant),
            CardType.ProbabilityBoost => L.T("+{0}%", id.Variant),
            CardType.GrantPermission => L.T("Guaranteed Approval"),
            CardType.SoftenPunishment => L.T("Soften Punishment"),
            CardType.CancelPunishment => L.T("Cancel Punishment"),
            _ => id.Type.ToString(),
        };
    }

    public static string DescriptionOf(CardId id, ILocalizer L)
    {
        return id.Type switch
        {
            CardType.CooldownBypass => L.T("Clears the cooldown standing between you and your next request."),
            CardType.DurationBoost => L.T("Extends the duration of your next granted request."),
            CardType.ProbabilityBoost => L.T("Improves your odds of approval on your next request."),
            CardType.GrantPermission => L.T("Your next request is approved without question — this once."),
            CardType.SoftenPunishment => L.T("Halves the severity of one pending punishment."),
            CardType.CancelPunishment => L.T("Removes one pending punishment outright."),
            _ => "",
        };
    }
}
