using Skjermvette.Core;
using Skjermvette.Core.Models;
using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Services;

/// <summary>Everything needed to work out a child's budget for the current day. <see cref="Child"/> is tracked.</summary>
public sealed record ChildDay(
    Child Child,
    Policy Policy,
    TimeWindow Day,
    TimeSpan Extra,
    IReadOnlyList<UsageEvent> Events,
    IReadOnlyList<Lease> ActiveLeases)
{
    /// <summary>The budget as one device sees it: other devices' offline allowances are set aside.</summary>
    public BudgetSnapshot SnapshotFor(Guid deviceId) =>
        BudgetCalculator.Calculate(Policy, Day, Events, ActiveLeases.Where(l => l.DeviceId != deviceId));
}
