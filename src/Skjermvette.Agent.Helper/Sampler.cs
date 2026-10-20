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
    /// <param name="browsers">
    /// The browser extension's reports. A browser in front counts as the website in its active tab, and a browser making
    /// sound as the websites in its tabs making sound. Without a report, or on a page that isn't a website, it counts as itself.
    /// </param>
    public static UsageKey[] InUse(string? foregroundPath, IEnumerable<string?> audioPaths, TimeSpan sinceLastInput, bool sessionActive,
        IReadOnlyCollection<BrowserReport>? browsers = null)
    {
        var audio = audioPaths.Select(ProcessName).OfType<string>().SelectMany(program =>
            Report(browsers, program) is { Audible.Length: > 0 } report ? report.Audible.Select(Website) : [Program(program)]);
        var foreground = sessionActive && sinceLastInput < IdleAfter ? ProcessName(foregroundPath) : null;
        var front = foreground is null ? null : Report(browsers, foreground)?.Active is { } active ? Website(active) : Program(foreground);
        return audio.Prepend(front).OfType<UsageKey>().Distinct().ToArray();
    }

    /// <summary>The time between two tick counts (milliseconds since boot), allowing for the count wrapping after 49.7 days.</summary>
    public static TimeSpan Elapsed(uint fromTick, uint toTick) => TimeSpan.FromMilliseconds(unchecked(toTick - fromTick));

    /// <summary>The first report from <paramref name="program"/>. With several profiles open, each reports on its own.</summary>
    private static BrowserReport? Report(IReadOnlyCollection<BrowserReport>? browsers, string program) =>
        browsers?.FirstOrDefault(b => b.Browser == program);

    private static UsageKey Program(string name) => new(MatcherKindEnum.Process, name);

    private static UsageKey Website(string domain) => new(MatcherKindEnum.Domain, domain);

    private static string? ProcessName(string? path) =>
        Path.GetFileName(path) is { Length: > 0 } name ? name.ToLowerInvariant() : null;
}
