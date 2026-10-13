using ScreenTime.Core.Models;

namespace ScreenTime.Core.DeviceApi;

/// <summary>
/// Everything a device needs to enforce on its own: the rules, the app catalog for mapping identifiers,
/// its current budget, and its offline allowance.
/// </summary>
public sealed record DeviceStateResponse(
    Guid DeviceId,
    Guid ChildId,
    Policy Policy,
    IReadOnlyList<LogicalApp> Catalog,
    BudgetSnapshot Snapshot,
    Lease? Lease);
