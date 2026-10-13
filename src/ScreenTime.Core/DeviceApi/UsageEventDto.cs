using ScreenTime.Core.Models;

namespace ScreenTime.Core.DeviceApi;

/// <summary>
/// One stretch of use. <see cref="Id"/> is generated on the device so re-sending is safe.
/// The server maps <see cref="Kind"/> and <see cref="RawIdentifier"/> to an app.
/// </summary>
public sealed record UsageEventDto(Guid Id, MatcherKindEnum Kind, string RawIdentifier, DateTimeOffset Start, double DurationSeconds);
