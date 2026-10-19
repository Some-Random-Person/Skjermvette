using Skjermvette.Core.Models;

namespace Skjermvette.Core.Tests;

/// <summary>The scenario from the requirements: 2 h total, YouTube max 30 min, Teams exempt.</summary>
internal static class Fixtures
{
    public static readonly Guid YouTube = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Teams = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    public static readonly Guid Minecraft = Guid.Parse("00000000-0000-0000-0000-0000000000a3");

    public static readonly Guid Phone = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    public static readonly Guid Pc = Guid.Parse("00000000-0000-0000-0000-0000000000d2");

    public static readonly TimeWindow Day = new(
        new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero));

    public static Policy Policy(TimeSpan? offlineAllowance = null) => new(
        DailyTotal: TimeSpan.FromHours(2),
        AppLimits: new Dictionary<Guid, TimeSpan> { [YouTube] = TimeSpan.FromMinutes(30) },
        ExemptAppIds: new HashSet<Guid> { Teams },
        OfflineAllowance: offlineAllowance ?? Models.Policy.DefaultOfflineAllowance,
        TimeZoneId: "UTC",
        ResetHour: 0);

    public static UsageEvent Use(Guid? app, double minutes, Guid? device = null, int startHour = 10) => new(
        Guid.NewGuid(),
        device ?? Pc,
        app,
        "raw",
        Day.Start.AddHours(startHour),
        TimeSpan.FromMinutes(minutes));

    public static TimeSpan Min(double minutes) => TimeSpan.FromMinutes(minutes);
}
