namespace ChastiMaid.Services;

public static class GameClock
{
    private static TimeSpan _offset = TimeSpan.Zero;

    public static DateTime Today => UtcNow().Date;

    public static DateTime UtcNow()
    {
        return DateTime.UtcNow + _offset;
    }

    public static void Advance(TimeSpan amount)
    {
        _offset += amount;
    }

    public static void Reset()
    {
        _offset = TimeSpan.Zero;
    }
}
