namespace Skjermvette.Server.Data.Entities;

/// <summary>A device's current offline allowance. Each device has at most one; renewing replaces it.</summary>
public class DeviceLease
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public TimeSpan Total { get; set; }
    public DateTime GrantedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public List<DeviceLeaseApp> Apps { get; } = [];
}
