namespace ChastiMaid.Services;

public static class GameClock
{
    private static TimeSpan _offset = TimeSpan.Zero;

    public static DateTime UtcNow() => DateTime.UtcNow + _offset;
    public static DateTime Today => UtcNow().Date;

    public static void Advance(TimeSpan amount) => _offset += amount;
    public static void Reset() => _offset = TimeSpan.Zero;
}
