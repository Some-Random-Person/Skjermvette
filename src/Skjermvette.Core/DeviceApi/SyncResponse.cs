using Skjermvette.Core.Models;

namespace Skjermvette.Core.DeviceApi;

/// <summary>The device's current budget and its renewed offline allowance.</summary>
public sealed record SyncResponse(BudgetSnapshot Snapshot, Lease Lease);
