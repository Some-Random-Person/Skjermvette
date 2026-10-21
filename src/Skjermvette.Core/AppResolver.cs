using Skjermvette.Core.Models;

namespace Skjermvette.Core;

/// <summary>Maps a raw identifier seen on a device (process, package, domain, ...) to a logical app.</summary>
public sealed class AppResolver
{
    private readonly Dictionary<(MatcherKindEnum, string), LogicalApp> _exact = [];
    private readonly Dictionary<string, LogicalApp> _domains = new(StringComparer.OrdinalIgnoreCase);

    public AppResolver(IEnumerable<LogicalApp> apps)
    {
        foreach (var app in apps)
        foreach (var matcher in app.Matchers)
        {
            if (matcher.Kind == MatcherKindEnum.Domain)
                _domains[NormalizeDomain(matcher.Value)] = app;
            else
                _exact[(matcher.Kind, Normalize(matcher.Kind, matcher.Value))] = app;
        }
    }

    public LogicalApp? Resolve(MatcherKindEnum kind, string rawIdentifier)
    {
        if (kind != MatcherKindEnum.Domain)
            return _exact.GetValueOrDefault((kind, Normalize(kind, rawIdentifier)));

        // Most specific match wins: music.youtube.com is tried before youtube.com.
        for (var host = NormalizeDomain(rawIdentifier); host.Length > 0; host = ParentDomain(host))
        {
            if (_domains.TryGetValue(host, out var app))
                return app;
        }
        return null;
    }

    private static string Normalize(MatcherKindEnum kind, string value) => kind switch
    {
        MatcherKindEnum.IosToken => value,
        // Split on both separators: Windows paths may be resolved on a Linux server.
        MatcherKindEnum.Process => value.Trim()[(value.Trim().LastIndexOfAny(['/', '\\']) + 1)..].ToLowerInvariant(),
        _ => value.Trim().ToLowerInvariant(),
    };

    /// <summary>A domain as it's matched: lowercase, without a trailing dot.</summary>
    public static string NormalizeDomain(string value) => value.Trim().TrimEnd('.').ToLowerInvariant();

    private static string ParentDomain(string host)
    {
        var dot = host.IndexOf('.');
        return dot < 0 ? "" : host[(dot + 1)..];
    }
}
