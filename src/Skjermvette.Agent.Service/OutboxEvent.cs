using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Service;

/// <summary>A usage event recorded on this device and not uploaded yet.</summary>
public class OutboxEvent
{
    public required Guid Id { get; set; }
    public required MatcherKindEnum Kind { get; set; }
    public required string RawIdentifier { get; set; }
    public required DateTime StartUtc { get; set; }
    public required double DurationSeconds { get; set; }
}
