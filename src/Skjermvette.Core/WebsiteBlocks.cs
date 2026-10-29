using System.Text.RegularExpressions;
using Skjermvette.Core.Models;

namespace Skjermvette.Core;

/// <summary>
/// Which websites a browser extension blocks, worked out by the agent from the budget the same way it decides which
/// programs to close. A domain includes its subdomains, and the most specific domain listed wins, as in
/// <see cref="AppResolver"/>. Any other website is blocked only when <see cref="BlockOthers"/> is set (the daily total is used up).
/// </summary>
/// <param name="Allowed">The domains of apps that aren't blocked, e.g. exempt apps, plus the dashboard.</param>
public sealed partial record WebsiteBlocks(bool BlockOthers, IReadOnlyList<BlockedWebsite> Blocked, IReadOnlyList<string> Allowed)
{
    /// <summary>Blocks nothing: before the first budget arrives, and once unpaired.</summary>
    public static readonly WebsiteBlocks Nothing = new(false, [], []);

    /// <param name="snapshot">What may be used now, or null when nothing is enforced.</param>
    /// <param name="catalog">The family's apps.</param>
    /// <param name="dashboard">The server's host name, which is never blocked.</param>
    public static WebsiteBlocks For(BudgetSnapshot? snapshot, IReadOnlyList<LogicalApp> catalog, string? dashboard)
    {
        if (snapshot is null)
            return Nothing;

        var apps = new AppResolver(catalog);
        var blocked = new List<BlockedWebsite>();
        var allowed = new List<string>();
        var domains = catalog.SelectMany(a => a.Matchers).Where(m => m.Kind == MatcherKindEnum.Domain)
            .Select(m => AppResolver.NormalizeDomain(m.Value)).Where(d => Domain().IsMatch(d)).Distinct();
        foreach (var domain in domains)
        {
            var app = apps.Resolve(MatcherKindEnum.Domain, domain)!;
            if (snapshot.IsBlocked(app.Id))
                blocked.Add(new BlockedWebsite(domain, snapshot.OwnLimitFirst(app.Id) ? app.Name : null));
            else
                allowed.Add(domain);
        }
        if (dashboard is not null && Domain().IsMatch(dashboard))
            allowed.Add(dashboard);
        return new WebsiteBlocks(snapshot.IsBlocked(null), blocked, allowed);
    }

    /// <summary>A lowercase host name, as the browser extension reports and matches them; anything else (e.g. a pattern) is left out.</summary>
    [GeneratedRegex("^[a-z0-9.-]{1,253}$")]
    private static partial Regex Domain();
}
