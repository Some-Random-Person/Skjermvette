using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Helper;

/// <summary>Decides what is in use from what Windows reports. Audio isn't tracked yet, so idle means nothing is in use.</summary>
public static class Sampler
{
    /// <summary>No keyboard or mouse input for this long means nobody is using the computer.</summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(2);

    /// <param name="foregroundPath">The path of the program in front, if any.</param>
    /// <param name="sinceLastInput">How long since the last keyboard or mouse input.</param>
    /// <param name="sessionActive">False while the session is locked or switched away from.</param>
    public static UsageKey[] InUse(string? foregroundPath, TimeSpan sinceLastInput, bool sessionActive) =>
        sessionActive && sinceLastInput < IdleAfter && ProcessName(foregroundPath) is { } name
            ? [new UsageKey(MatcherKindEnum.Process, name)]
            : [];

    /// <summary>The time between two tick counts (milliseconds since boot), allowing for the count wrapping after 49.7 days.</summary>
    public static TimeSpan Elapsed(uint fromTick, uint toTick) => TimeSpan.FromMilliseconds(unchecked(toTick - fromTick));

    private static string? ProcessName(string? path) =>
        Path.GetFileName(path) is { Length: > 0 } name ? name.ToLowerInvariant() : null;
}
