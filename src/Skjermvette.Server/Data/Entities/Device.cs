using Skjermvette.Core.DeviceApi;

namespace Skjermvette.Server.Data.Entities;

public class Device
{
    public Guid Id { get; set; }
    public Guid ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public required string Name { get; set; }
    public DevicePlatformEnum Platform { get; set; }

    /// <summary>SHA-256 of the device token. The token itself is only shown to the device once.</summary>
    public required string TokenHash { get; set; }

    public DateTime PairedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }

    /// <summary>
    /// Set when the parent unpairs the device. The row is kept so its usage still counts, but the device
    /// is hidden everywhere and its token no longer works.
    /// </summary>
    public DateTime? UnpairedAtUtc { get; set; }

    /// <summary>Overrides the child's offline allowance for this device when set.</summary>
    public TimeSpan? OfflineAllowanceOverride { get; set; }

    public DeviceLease? Lease { get; set; }
}
