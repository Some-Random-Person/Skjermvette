namespace Skjermvette.Core.Models;

/// <summary>A child's daily rules. An app that is both limited and exempt is treated as exempt.</summary>
public sealed record Policy(
    TimeSpan DailyTotal,
    IReadOnlyDictionary<Guid, TimeSpan> AppLimits,
    IReadOnlyCollection<Guid> ExemptAppIds,
    TimeSpan OfflineAllowance,
    string TimeZoneId,
    int ResetHour)
{
    public static readonly TimeSpan DefaultOfflineAllowance = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxOfflineAllowance = TimeSpan.FromMinutes(60);
}
