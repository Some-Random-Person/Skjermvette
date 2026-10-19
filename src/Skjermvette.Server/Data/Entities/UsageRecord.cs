using Skjermvette.Core.Models;

namespace Skjermvette.Server.Data.Entities;

/// <summary>A usage event as reported by a device. <see cref="Id"/> is generated on the device.</summary>
public class UsageRecord
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public Guid ChildId { get; set; }

    /// <summary>Null until the identifier is mapped to an app; the time still counts toward the total.</summary>
    public Guid? AppDefinitionId { get; set; }

    public MatcherKindEnum Kind { get; set; }
    public required string RawIdentifier { get; set; }
    public DateTime StartUtc { get; set; }
    public TimeSpan Duration { get; set; }
}
