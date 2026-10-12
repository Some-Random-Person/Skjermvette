using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ScreenTime.Core;
using ScreenTime.Core.Models;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Services;

public class BudgetService(AppDbContext db, TimeProvider time, IHubContext<BudgetHub> hub, ChildLocks locks)
{
    /// <summary>Usage events longer than this are rejected, so loading from (day start − this) finds every overlapping event.</summary>
    public static readonly TimeSpan MaxEventDuration = TimeSpan.FromHours(1);

    public async Task<ChildDay> LoadDayAsync(Guid childId, CancellationToken ct)
    {
        var child = await db.Children
            .Include(c => c.AppRules)
            .Include(c => c.Devices).ThenInclude(d => d.Lease!).ThenInclude(l => l.Apps)
            .AsSplitQuery()
            .FirstAsync(c => c.Id == childId, ct);

        var now = time.GetUtcNow();
        var day = TimeWindow.Day(now, child.TimeZoneId, child.ResetHour);
        var dayStart = day.Start.UtcDateTime;
        var dayEnd = day.End.UtcDateTime;
        var loadFrom = dayStart - MaxEventDuration;

        var grants = await db.ExtraTimeGrants.AsNoTracking()
            .Where(g => g.ChildId == childId && g.DayStartUtc == dayStart)
            .Select(g => g.Amount)
            .ToListAsync(ct);
        var extra = grants.Aggregate(TimeSpan.Zero, (a, b) => a + b);

        var events = await db.UsageRecords.AsNoTracking()
            .Where(u => u.ChildId == childId && u.StartUtc >= loadFrom && u.StartUtc < dayEnd)
            .Select(u => new UsageEvent(u.Id, u.DeviceId, u.AppDefinitionId, u.RawIdentifier, new DateTimeOffset(u.StartUtc), u.Duration))
            .ToListAsync(ct);

        var leases = child.Devices
            .Where(d => d.Lease is { } l && l.ExpiresAtUtc > now.UtcDateTime)
            .Select(d => ToCore(d.Lease!))
            .ToList();

        return new ChildDay(child, ToPolicy(child, extra), day, extra, events, leases);
    }

    /// <summary>
    /// Records the device's usage (ignoring events it already sent), renews its offline allowance, and
    /// pushes the new budget to all of the child's devices. Devices call this with every report.
    /// </summary>
    public Task<SyncResponse> SyncAsync(Guid deviceId, Guid childId, Guid familyId, IReadOnlyList<UsageEventDto> events, CancellationToken ct) =>
        locks.RunAsync(childId, async () =>
        {
            await RecordUsageAsync(deviceId, childId, familyId, events, ct);

            var day = await LoadDayAsync(childId, ct);
            var device = day.Child.Devices.First(d => d.Id == deviceId);
            device.LastSeenAtUtc = time.GetUtcNow().UtcDateTime;

            var snapshot = day.SnapshotFor(deviceId);
            var allowance = device.OfflineAllowanceOverride ?? day.Child.OfflineAllowance;
            var lease = Lease.Grant(deviceId, snapshot, allowance, time.GetUtcNow(), day.Day);
            SaveLease(device, lease);
            await db.SaveChangesAsync(ct);

            day = day with { ActiveLeases = [.. day.ActiveLeases.Where(l => l.DeviceId != deviceId), lease] };
            await PushAsync(day, ct);
            return new SyncResponse(snapshot, lease);
        }, ct);

    /// <summary>Sends every device of the child its own view of the budget.</summary>
    public async Task PushAsync(ChildDay day, CancellationToken ct)
    {
        foreach (var device in day.Child.Devices)
            await hub.Clients.Group(BudgetHub.DeviceGroup(device.Id)).SendAsync(BudgetHub.BudgetUpdated, day.SnapshotFor(device.Id), ct);
    }

    public async Task PushAsync(Guid childId, CancellationToken ct) => await PushAsync(await LoadDayAsync(childId, ct), ct);

    private async Task RecordUsageAsync(Guid deviceId, Guid childId, Guid familyId, IReadOnlyList<UsageEventDto> events, CancellationToken ct)
    {
        if (events.Count == 0)
            return;

        var ids = events.Select(e => e.Id).ToList();
        var known = await db.UsageRecords.Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        var resolver = new AppResolver((await db.VisibleAppsAsync(familyId, ct)).ToCoreCatalog());

        foreach (var e in events.Where(e => !known.Contains(e.Id)).DistinctBy(e => e.Id))
        {
            db.UsageRecords.Add(new UsageRecord
            {
                Id = e.Id,
                DeviceId = deviceId,
                ChildId = childId,
                AppDefinitionId = resolver.Resolve(e.Kind, e.RawIdentifier)?.Id,
                Kind = e.Kind,
                RawIdentifier = e.RawIdentifier,
                StartUtc = e.Start.UtcDateTime,
                Duration = TimeSpan.FromSeconds(e.DurationSeconds),
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private void SaveLease(Device device, Lease lease)
    {
        if (device.Lease is null)
        {
            device.Lease = new DeviceLease();
            db.DeviceLeases.Add(device.Lease);
        }

        device.Lease.Total = lease.Total;
        device.Lease.GrantedAtUtc = lease.GrantedAt.UtcDateTime;
        device.Lease.ExpiresAtUtc = lease.ExpiresAt.UtcDateTime;
        db.RemoveRange(device.Lease.Apps);
        device.Lease.Apps.Clear();
        device.Lease.Apps.AddRange(lease.Apps.Select(a => new DeviceLeaseApp { AppDefinitionId = a.Key, Amount = a.Value }));
    }

    public static Policy ToPolicy(Child child, TimeSpan extra) => new(
        child.DailyTotal + extra,
        child.AppRules.Where(r => r.Kind == AppRuleKindEnum.Limited && r.Limit is not null)
            .ToDictionary(r => r.AppDefinitionId, r => r.Limit!.Value),
        child.AppRules.Where(r => r.Kind == AppRuleKindEnum.Exempt).Select(r => r.AppDefinitionId).ToHashSet(),
        child.OfflineAllowance,
        child.TimeZoneId,
        child.ResetHour);

    public static Lease ToCore(DeviceLease lease) => new(
        lease.DeviceId,
        lease.Total,
        lease.Apps.ToDictionary(a => a.AppDefinitionId, a => a.Amount),
        new DateTimeOffset(lease.GrantedAtUtc),
        new DateTimeOffset(lease.ExpiresAtUtc));
}
