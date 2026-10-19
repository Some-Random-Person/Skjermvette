using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Helper;

/// <summary>Decides what is in use from what Windows reports: the program in front, plus any program making sound.</summary>
public static class Sampler
{
    /// <summary>No keyboard or mouse input for this long means nobody is at the computer: only programs making sound count then.</summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(2);

    /// <param name="foregroundPath">The path of the program in front, if any.</param>
    /// <param name="audioPaths">The paths of the programs making sound.</param>
    /// <param name="sinceLastInput">How long since the last keyboard or mouse input.</param>
    /// <param name="sessionActive">False while the session is locked or switched away from: only sound counts then.</param>
    public static UsageKey[] InUse(string? foregroundPath, IEnumerable<string?> audioPaths, TimeSpan sinceLastInput, bool sessionActive)
    {
        var audio = audioPaths.Select(ProcessName).OfType<string>().ToList();
        var foreground = sessionActive && sinceLastInput < IdleAfter ? ProcessName(foregroundPath) : null;
        return audio.Prepend(foreground).OfType<string>().Distinct().Select(name => new UsageKey(MatcherKindEnum.Process, name)).ToArray();
    }

    /// <summary>The time between two tick counts (milliseconds since boot), allowing for the count wrapping after 49.7 days.</summary>
    public static TimeSpan Elapsed(uint fromTick, uint toTick) => TimeSpan.FromMilliseconds(unchecked(toTick - fromTick));

    private static string? ProcessName(string? path) =>
        Path.GetFileName(path) is { Length: > 0 } name ? name.ToLowerInvariant() : null;
}
