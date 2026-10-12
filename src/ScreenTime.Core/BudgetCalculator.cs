using ScreenTime.Core.Models;

namespace ScreenTime.Core;

public static class BudgetCalculator
{
    /// <summary>
    /// Today's budget as seen by one device. Usage from every device counts (time is added up, not merged).
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
        var totalUsed = TimeSpan.Zero;
        var appUsed = new Dictionary<Guid, TimeSpan>();

        foreach (var e in events)
        {
            if (e.LogicalAppId is { } exempt && exemptAppIds.Contains(exempt))
                continue;

            var counted = window.Overlap(e.Start, e.End);
            totalUsed += counted;
            if (e.LogicalAppId is { } appId)
                appUsed[appId] = appUsed.GetValueOrDefault(appId) + counted;
        }

        var apps = appLimits
            .Where(kv => !exemptAppIds.Contains(kv.Key))
            .Select(kv => new AppBudget(kv.Key, kv.Value, appUsed.GetValueOrDefault(kv.Key), reservedApps.GetValueOrDefault(kv.Key)))
            .ToList();

        return new BudgetSnapshot(totalLimit, totalUsed, reservedTotal, apps, exemptAppIds);
    }

    private static TimeSpan Sum(IEnumerable<TimeSpan> values) => values.Aggregate(TimeSpan.Zero, (a, b) => a + b);
}
