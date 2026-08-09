namespace ChastiMaid.Models;

/// <summary>
///     The combined effect of the permission cards played (manually staged or suspended from the Fortune Deck)
///     on a single permission request. Multiple cards of the same effect stack algebraically before being
///     applied by <see cref="Repositories.SaveRepository.RequestPermission" />.
/// </summary>
public readonly record struct PermissionModifiers(
    bool BypassCooldown,
    int ProbabilityBonusPercent,
    int DurationBonusMinutes,
    bool GuaranteedGrant)
{
    public static readonly PermissionModifiers None = default;

    public PermissionModifiers CombinedWith(PermissionModifiers other)
    {
        return new PermissionModifiers(
            BypassCooldown || other.BypassCooldown,
            ProbabilityBonusPercent + other.ProbabilityBonusPercent,
            DurationBonusMinutes + other.DurationBonusMinutes,
            GuaranteedGrant || other.GuaranteedGrant);
    }
}
