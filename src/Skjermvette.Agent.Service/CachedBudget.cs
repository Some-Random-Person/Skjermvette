using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Service;

/// <summary>What the device needs to keep counting while offline, also across restarts (<c>budget.json</c>).</summary>
public sealed record CachedBudget(IReadOnlyList<LogicalApp> Catalog, IReadOnlyCollection<Guid> ExemptAppIds, Lease? Lease);
