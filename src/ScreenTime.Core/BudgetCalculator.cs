using ScreenTime.Core.Models;

namespace ScreenTime.Core;

public static class BudgetCalculator
{
    /// <summary>
    /// Today's budget as seen by one device. Usage from every device counts, but time when several apps or devices
    /// are in use at once counts only once, for the total and for each app's limit.
    /// <paramref name="otherDevicesLeases"/> are the active offline allowances of the child's other devices.
    /// </summary>
    public static BudgetSnapshot Calculate(
        Policy policy,
        TimeWindow day,
        IEnumerable<UsageEvent> events,
        IEnumerable<Lease> otherDevicesLeases)
    {
        var leases = otherDevicesLeases.ToList();
        var reservedApps = leases
            .SelectMany(l => l.Apps)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => Sum(g.Select(kv => kv.Value)));

        return Build(policy.DailyTotal, policy.AppLimits, policy.ExemptAppIds, day, events,
            Sum(leases.Select(l => l.Total)), reservedApps);
    }

    /// <summary>
    /// The budget of a device that is offline: only its own lease, spent by what it used since the lease was granted.
    /// </summary>
    public static BudgetSnapshot CalculateOffline(Lease lease, IReadOnlyCollection<Guid> exemptAppIds, IEnumerable<UsageEvent> offlineEvents) =>
        Build(lease.Total, lease.Apps, exemptAppIds, new TimeWindow(lease.GrantedAt, lease.ExpiresAt), offlineEvents,
            TimeSpan.Zero, new Dictionary<Guid, TimeSpan>());

    private static BudgetSnapshot Build(
        TimeSpan totalLimit,
        IReadOnlyDictionary<Guid, TimeSpan> appLimits,
        IReadOnlyCollection<Guid> exemptAppIds,
        TimeWindow window,
        IEnumerable<UsageEvent> events,
        TimeSpan reservedTotal,
        IReadOnlyDictionary<Guid, TimeSpan> reservedApps)
    {
        var counted = events
            .Where(e => e.LogicalAppId is not { } id || !exemptAppIds.Contains(id))
            .ToList();
        var totalUsed = UsedTime(counted, window);
        var appUsed = counted
            .Where(e => e.LogicalAppId is not null)
            .GroupBy(e => e.LogicalAppId!.Value)
            .ToDictionary(g => g.Key, g => UsedTime(g, window));

        var apps = appLimits
            .Where(kv => !exemptAppIds.Contains(kv.Key))
            .Select(kv => new AppBudget(kv.Key, kv.Value, appUsed.GetValueOrDefault(kv.Key), reservedApps.GetValueOrDefault(kv.Key)))
            .ToList();

        return new BudgetSnapshot(totalLimit, totalUsed, reservedTotal, apps, exemptAppIds);
    }

    /// <summary>
    /// Wall-clock time covered by <paramref name="events"/> within <paramref name="window"/>. Overlapping events
    /// (several apps or devices in use at once) count once: one minute is one minute.
    /// </summary>
    public static TimeSpan UsedTime(IEnumerable<UsageEvent> events, TimeWindow window)
    {
        var used = TimeSpan.Zero;
        DateTimeOffset? runStart = null, runEnd = null;

        foreach (var (start, end) in events
            .Select(e => (Start: Max(e.Start, window.Start), End: Min(e.End, window.End)))
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start))
        {
            if (runEnd is { } current && start <= current)
            {
                if (end > current)
                    runEnd = end;
                continue;
            }
            if (runStart is { } s && runEnd is { } e)
                used += e - s;
            runStart = start;
            runEnd = end;
        }
        if (runStart is { } lastStart && runEnd is { } lastEnd)
            used += lastEnd - lastStart;
        return used;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static TimeSpan Sum(IEnumerable<TimeSpan> values) => values.Aggregate(TimeSpan.Zero, (a, b) => a + b);
}
