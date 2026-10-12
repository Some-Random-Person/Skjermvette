namespace ScreenTime.Core.Models;

/// <summary>
/// A stretch of foreground use on one device. <see cref="LogicalAppId"/> is null when the
/// raw identifier isn't mapped to an app yet; that time still counts toward the daily total.
/// </summary>
public sealed record UsageEvent(
    Guid Id,
    Guid DeviceId,
    Guid? LogicalAppId,
    string RawIdentifier,
    DateTimeOffset Start,
    TimeSpan Duration)
{
    public DateTimeOffset End => Start + Duration;
}
