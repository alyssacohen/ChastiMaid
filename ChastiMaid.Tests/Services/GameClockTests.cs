using ChastiMaid.Services;

namespace ChastiMaid.Tests.Services;

public class GameClockTests : IDisposable
{
    public void Dispose() => GameClock.Reset();

    [Fact]
    public void UtcNow_WithNoOffset_IsCloseToRealTime()
    {
        var before = DateTime.UtcNow;
        var clockTime = GameClock.UtcNow();
        var after = DateTime.UtcNow;

        Assert.InRange(clockTime, before, after);
    }

    [Fact]
    public void Advance_ShiftsTimeForward()
    {
        var before = GameClock.UtcNow();
        GameClock.Advance(TimeSpan.FromDays(1));

        Assert.True(GameClock.UtcNow() > before.AddHours(23));
    }

    [Fact]
    public void Reset_RestoresRealTime()
    {
        GameClock.Advance(TimeSpan.FromDays(30));
        GameClock.Reset();

        var delta = Math.Abs((GameClock.UtcNow() - DateTime.UtcNow).TotalSeconds);
        Assert.True(delta < 1);
    }

    [Fact]
    public void Today_MatchesDatePartOfUtcNow()
    {
        GameClock.Advance(TimeSpan.FromHours(48));

        Assert.Equal(GameClock.UtcNow().Date, GameClock.Today);
    }

    [Fact]
    public void Advance_IsAdditive()
    {
        GameClock.Advance(TimeSpan.FromDays(1));
        GameClock.Advance(TimeSpan.FromDays(1));

        var delta = GameClock.UtcNow() - DateTime.UtcNow;
        Assert.True(delta.TotalHours > 47 && delta.TotalHours < 49);
    }
}
