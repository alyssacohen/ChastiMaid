using LiteDB;

namespace ChastiMaid.Models;

public enum SuspendedEffect
{
    Probability,
    Duration,
    Grant,
}

/// <summary>
///     A permission-targeting buff/debuff won from the Fortune Deck, waiting for the player to request that
///     specific permission kind. Consumed (and folded into <see cref="PermissionModifiers" />) the moment a
///     matching request is submitted — see docs/specs/fortune-deck.md "Resolution mechanic for permission-targeting
///     buffs/debuffs". <see cref="Value" /> carries the sign for Probability/Duration; ignored for Grant.
/// </summary>
public class SuspendedModifier
{
    [BsonId] public int Id { get; set; }
    public PermissionKind Kind { get; set; }
    public SuspendedEffect Effect { get; set; }
    public int Value { get; set; }
}
