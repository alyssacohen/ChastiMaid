using ChastiMaid.Services;

namespace ChastiMaid.Tests.Services;

public class GameDayTests
{
    // Rome: UTC+1 in winter, UTC+2 in summer; the 2026 switch to summer time is 29 March at 01:00 UTC.
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static DateTime Utc(int month, int day, int hour, int minute) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void AfterLocalMidnight_IsANewDay_EvenOnTheSameUtcDate()
    {
        // 23:00 and 00:30 local time: the UTC date is 15 January for both.
        Assert.False(GameDay.IsSameDay(Utc(1, 15, 22, 0), Utc(1, 15, 23, 30), Rome));
    }

    [Fact]
    public void SameLocalDay_IsTheSameDay_AcrossUtcMidnight()
    {
        // 00:30 and 12:00 local time on 16 July: the first instant is still 15 July in UTC.
        Assert.True(GameDay.IsSameDay(Utc(7, 15, 22, 30), Utc(7, 16, 10, 0), Rome));
    }

    [Fact]
    public void DaylightSavingDay_EndsAtLocalMidnight()
    {
        var earlyMorning = Utc(3, 28, 23, 30); // 00:30 local on 29 March, still winter time
        var lateEvening = Utc(3, 29, 21, 30); // 23:30 local on 29 March, now summer time
        var nextDay = Utc(3, 29, 22, 30); // 00:30 local on 30 March

        Assert.True(GameDay.IsSameDay(earlyMorning, lateEvening, Rome));
        Assert.False(GameDay.IsSameDay(lateEvening, nextDay, Rome));
    }
}
