using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Contracts;

/// <summary><see cref="SetAsideMinutes"/> is the device's current offline allowance (time set aside for it).</summary>
public sealed record DeviceResponse(
    Guid Id,
    Guid ChildId,
    string Name,
    DevicePlatformEnum Platform,
    DateTime PairedAtUtc,
    DateTime? LastSeenAtUtc,
    bool IsOnline,
    int? OfflineAllowanceOverrideMinutes,
    double SetAsideMinutes)
{
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(60);

    public static DeviceResponse From(Device device, DateTime nowUtc) => new(
        device.Id,
        device.ChildId,
        device.Name,
        device.Platform,
        device.PairedAtUtc,
        device.LastSeenAtUtc,
        device.LastSeenAtUtc is { } seen && nowUtc - seen < OnlineWindow,
        device.OfflineAllowanceOverride is { } o ? (int)o.TotalMinutes : null,
        device.Lease is { } lease && lease.ExpiresAtUtc > nowUtc ? lease.Total.TotalMinutes : 0);
}
