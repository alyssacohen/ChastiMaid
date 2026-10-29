namespace ChastiMaid.Services;

/// <summary>
///     The game's notion of "a day": the player's local calendar day, starting at local midnight. Instants stay
///     stored in UTC and are converted only when compared, so a daylight-saving change never skips or repeats
///     a day.
/// </summary>
public static class GameDay
{
    /// <summary>True when both instants fall on the same local calendar day of this machine.</summary>
    public static bool IsSameDay(DateTime a, DateTime b) => IsSameDay(a, b, TimeZoneInfo.Local);

    /// <summary>True when both instants fall on the same calendar day in <paramref name="zone" />.</summary>
    public static bool IsSameDay(DateTime a, DateTime b, TimeZoneInfo zone) =>
        // ConvertTime honours each value's Kind (Utc, or Local as LiteDB returns it without the app's mapper).
        TimeZoneInfo.ConvertTime(a, zone).Date == TimeZoneInfo.ConvertTime(b, zone).Date;
}
