using System.ComponentModel;
using System.Diagnostics;
using Skjermvette.Core;
using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Service;

/// <summary>
/// Checks what is in use against the budget, on every sample from the Helper: warns the child when an app or the daily
/// total is about to run out, and closes programs that are in use once their time is up. Programs that are only running
/// in the background aren't counted, so they're left alone.
/// </summary>
/// <param name="close">Closes every process with a program name in the child's session and returns how many it closed.</param>
public sealed class Enforcer(BudgetState state, ILogger<Enforcer> logger, Func<string, int>? close = null)
{
    /// <summary>How long before running out the child is warned.</summary>
    public static readonly TimeSpan[] WarnAt = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)];

    /// <summary>Windows itself (the desktop, Start, Settings, the lock screen), which is never closed.</summary>
    public static readonly IReadOnlySet<string> AlwaysAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "lockapp.exe", "systemsettings.exe", "applicationframehost.exe", "searchhost.exe",
        "startmenuexperiencehost.exe", "shellexperiencehost.exe", "textinputhost.exe", "skjermvette.agent.helper.exe",
    };

    private readonly Func<string, int> _close = close ?? (program => CloseProcesses(program, ConsoleSession.Id()));

    /// <summary>The thresholds in <see cref="WarnAt"/> already warned about, per app (null for the daily total).</summary>
    private readonly HashSet<(Guid? App, TimeSpan At)> _warned = [];

    /// <summary>What to tell the child about <paramref name="inUse"/>, after closing whatever has run out.</summary>
    public List<HelperNotice> Check(IReadOnlyCollection<UsageKey> inUse)
    {
        var notices = new List<HelperNotice>();
        if (state.Snapshot is not { } snapshot)
            return notices;

        foreach (var key in inUse)
        {
            if (key.Kind == MatcherKindEnum.Process && AlwaysAllowed.Contains(key.Identifier))
                continue;
            var app = state.Apps?.Resolve(key.Kind, key.Identifier);
            if (snapshot.Remaining(app?.Id) is not { } remaining)
                continue;

            // The app's own limit, unless the total runs out first.
            var limitedBy = app is not null && snapshot.OwnLimitFirst(app.Id) ? app : null;

            if (remaining > TimeSpan.Zero)
            {
                if (Warning(limitedBy, remaining) is { } warning)
                    notices.Add(warning);
            }
            else if (key.Kind == MatcherKindEnum.Process && _close(key.Identifier) > 0)
            {
                logger.LogInformation("Closed {Program}: {Reason}.", key.Identifier, limitedBy is null ? "the daily total is used up" : $"{limitedBy.Name} is used up");
                notices.Add(new HelperNotice(HelperNoticeEnum.Closed, limitedBy?.Name, 0));
            }
        }
        return notices.Distinct().ToList();
    }

    private HelperNotice? Warning(LogicalApp? app, TimeSpan remaining)
    {
        // Forget thresholds no longer reached, e.g. after a parent added time, so they're warned about again.
        _warned.RemoveWhere(w => w.App == app?.Id && remaining > w.At);
        var reached = WarnAt.Where(at => remaining <= at).ToList();
        if (reached.Count == 0 || _warned.Contains((app?.Id, reached.Min())))
            return null;

        foreach (var at in reached)
            _warned.Add((app?.Id, at));
        return new HelperNotice(HelperNoticeEnum.Warning, app?.Name, (int)Math.Ceiling(remaining.TotalMinutes));
    }

    /// <summary>The websites the browser extension blocks now. Websites can't be closed here, so the extension blocks them instead.</summary>
    public WebsiteBlocks Websites() => WebsiteBlocks.For(state.Snapshot, state.Catalog, state.Dashboard);

    /// <summary>Closes every process with a program name in a session and returns how many it closed.</summary>
    public static int CloseProcesses(string program, int? session)
    {
        var closed = 0;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(program)))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != session)
                        continue;
                    process.Kill();
                    closed++;
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Already gone, or not ours to close.
                }
            }
        }
        return closed;
    }
}
