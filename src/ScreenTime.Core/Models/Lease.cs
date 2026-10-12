namespace ScreenTime.Core.Models;

/// <summary>
/// A device's offline allowance: time set aside for it so it can keep working without a connection.
/// <see cref="Total"/> caps all non-exempt use; <see cref="Apps"/> caps each limited app.
/// </summary>
public sealed record Lease(
    Guid DeviceId,
    TimeSpan Total,
    IReadOnlyDictionary<Guid, TimeSpan> Apps,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// Sets aside up to <paramref name="allowance"/> of what <paramref name="snapshot"/> (this device's view,
    /// with other devices' leases already subtracted) has left. Expires at the end of <paramref name="day"/>.
    /// </summary>
    public static Lease Grant(Guid deviceId, BudgetSnapshot snapshot, TimeSpan allowance, DateTimeOffset now, TimeWindow day)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(allowance, TimeSpan.Zero);

        var apps = snapshot.Apps
            .Where(a => !snapshot.ExemptAppIds.Contains(a.AppId))
            .ToDictionary(a => a.AppId, a => Min(allowance, snapshot.Remaining(a.AppId)!.Value));

        return new Lease(deviceId, Min(allowance, snapshot.TotalRemaining), apps, now, day.End);
    }

    public bool IsActive(DateTimeOffset now) => now >= GrantedAt && now < ExpiresAt;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
