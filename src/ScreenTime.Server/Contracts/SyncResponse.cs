using ScreenTime.Core.Models;

namespace ScreenTime.Server.Contracts;

/// <summary>The device's current budget and its renewed offline allowance.</summary>
public sealed record SyncResponse(BudgetSnapshot Snapshot, Lease Lease);
