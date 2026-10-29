namespace Skjermvette.Core.Models;

/// <summary>
/// What one device may still use. <see cref="TotalReserved"/> and <see cref="AppBudget.Reserved"/>
/// are the offline allowances currently set aside for the child's other devices.
/// </summary>
public sealed record BudgetSnapshot(
    TimeSpan TotalLimit,
    TimeSpan TotalUsed,
    TimeSpan TotalReserved,
    IReadOnlyList<AppBudget> Apps,
    IReadOnlyCollection<Guid> ExemptAppIds)
{
    public TimeSpan TotalRemaining => AppBudget.Clamp(TotalLimit - TotalUsed - TotalReserved);

    /// <summary>Time left for an app, or null when the app is exempt (unlimited). Pass null for unmapped apps.</summary>
    public TimeSpan? Remaining(Guid? appId)
    {
        if (appId is { } id && ExemptAppIds.Contains(id))
            return null;

        var app = appId is null ? null : Apps.FirstOrDefault(a => a.AppId == appId);
        return app is null || app.Remaining > TotalRemaining ? TotalRemaining : app.Remaining;
    }

    public bool IsBlocked(Guid? appId) => Remaining(appId) is { } remaining && remaining <= TimeSpan.Zero;

    /// <summary>True when the app's own limit runs out before the daily total, so the app is what to name to the child.</summary>
    public bool OwnLimitFirst(Guid appId) => Apps.FirstOrDefault(a => a.AppId == appId) is { } app && app.Remaining < TotalRemaining;
}
