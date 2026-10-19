using Skjermvette.Core.Models;

namespace Skjermvette.Server.Contracts;

/// <summary>An identifier seen on a device that isn't mapped to any app yet.</summary>
public sealed record UnassignedIdentifierResponse(MatcherKindEnum Kind, string RawIdentifier, double MinutesLast7Days, DateTime LastSeenUtc);
