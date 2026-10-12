namespace ScreenTime.Core.Models;

/// <summary>Budget for one limited app. <see cref="Reserved"/> is time set aside for other devices.</summary>
public sealed record AppBudget(Guid AppId, TimeSpan Limit, TimeSpan Used, TimeSpan Reserved)
{
    public TimeSpan Remaining => Clamp(Limit - Used - Reserved);

    internal static TimeSpan Clamp(TimeSpan value) => value > TimeSpan.Zero ? value : TimeSpan.Zero;
}
