using System.ComponentModel;

namespace ScreenTime.Server.Data.Entities;

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

    /// <summary>Overrides the child's offline allowance for this device when set.</summary>
    public TimeSpan? OfflineAllowanceOverride { get; set; }

    public DeviceLease? Lease { get; set; }
}

public enum DevicePlatformEnum
{
    [Description("Windows")]
    Windows = 1,

    [Description("Android")]
    Android = 2,

    [Description("iOS / iPadOS")]
    Ios = 3,

    [Description("macOS")]
    MacOs = 4,

    [Description("Linux")]
    Linux = 5,
}
