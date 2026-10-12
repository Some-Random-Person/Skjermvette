namespace ScreenTime.Core.Models;

/// <summary>
/// What one device may still use. <see cref="TotalReserved"/> and <see cref="AppBudget.Reserved"/>
/// are the offline allowances currently set aside for the child's other devices.
/// </summary>
public sealed record BudgetSnapshot(
    TimeSpan TotalLimit,
    TimeSpan TotalUsed,
    TimeSpan TotalReserved,
    IReadOnlyList<AppBudget> Apps,
    IReadOnlySet<Guid> ExemptAppIds)
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
}
