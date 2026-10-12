using ScreenTime.Core.Models;

namespace ScreenTime.Core.Tests;

public class TimeWindowTests
{
    private const string Oslo = "Europe/Oslo";

    [Fact]
    public void Day_StartsAtResetHourInChildsTimeZone()
    {
        // 12 Oct 2026, 10:00 in Oslo (UTC+2, summer time).
        var now = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

        var day = TimeWindow.Day(now, Oslo, resetHour: 4);

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 4, 0, 0, TimeSpan.FromHours(2)), day.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.FromHours(2)), day.End);
    }

    [Fact]
    public void Day_BeforeResetHour_BelongsToPreviousDay()
    {
        // 12 Oct 2026, 03:00 in Oslo, before the 04:00 reset.
        var now = new DateTimeOffset(2026, 10, 12, 1, 0, 0, TimeSpan.Zero);

        var day = TimeWindow.Day(now, Oslo, resetHour: 4);

        Assert.Equal(new DateTimeOffset(2026, 10, 11, 4, 0, 0, TimeSpan.FromHours(2)), day.Start);
    }

    [Fact]
    public void Day_AcrossDstEnd_Is25Hours()
    {
        // Oslo leaves summer time on 25 Oct 2026.
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.FromHours(1));

        var day = TimeWindow.Day(now, Oslo, resetHour: 0);

        Assert.Equal(TimeSpan.FromHours(25), day.End - day.Start);
    }

    [Fact]
    public void Day_ResetHourInDstGap_UsesFirstValidTime()
    {
        // 02:00 doesn't exist in Oslo on 29 Mar 2026; clocks jump to 03:00.
        var now = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.FromHours(2));

        var day = TimeWindow.Day(now, Oslo, resetHour: 2);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 3, 0, 0, TimeSpan.FromHours(2)), day.Start);
    }

    [Fact]
    public void Overlap_ClipsToWindow()
    {
        var window = new TimeWindow(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1));

        Assert.Equal(TimeSpan.FromMinutes(30), window.Overlap(DateTimeOffset.UnixEpoch.AddMinutes(30), DateTimeOffset.UnixEpoch.AddHours(2)));
        Assert.Equal(TimeSpan.Zero, window.Overlap(DateTimeOffset.UnixEpoch.AddHours(2), DateTimeOffset.UnixEpoch.AddHours(3)));
    }
}
