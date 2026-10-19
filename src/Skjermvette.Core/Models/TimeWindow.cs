namespace Skjermvette.Core.Models;

/// <summary>A half-open interval [Start, End).</summary>
public sealed record TimeWindow(DateTimeOffset Start, DateTimeOffset End)
{
    /// <summary>
    /// The screen-time day containing <paramref name="now"/>: from <paramref name="resetHour"/> local time
    /// to the same hour the next day. Days around DST changes are 23 or 25 hours long.
    /// </summary>
    public static TimeWindow Day(DateTimeOffset now, string timeZoneId, int resetHour)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(resetHour);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resetHour, 23);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localDate = TimeZoneInfo.ConvertTime(now, zone).DateTime.Date;

        var start = ResetInstant(localDate, zone, resetHour);
        if (start > now)
            start = ResetInstant(localDate.AddDays(-1), zone, resetHour);

        return new TimeWindow(start, ResetInstant(TimeZoneInfo.ConvertTime(start, zone).DateTime.Date.AddDays(1), zone, resetHour));
    }

    public TimeSpan Overlap(DateTimeOffset start, DateTimeOffset end)
    {
        var from = start > Start ? start : Start;
        var to = end < End ? end : End;
        return to > from ? to - from : TimeSpan.Zero;
    }

    private static DateTimeOffset ResetInstant(DateTime localDate, TimeZoneInfo zone, int resetHour)
    {
        var local = localDate.AddHours(resetHour);
        // A reset hour that falls in a spring-forward gap doesn't exist; use the first valid time after it.
        while (zone.IsInvalidTime(local))
            local = local.AddMinutes(30);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
