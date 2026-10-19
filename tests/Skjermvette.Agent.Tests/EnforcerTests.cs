using Microsoft.Extensions.Logging.Abstractions;
using Skjermvette.Agent.Service;
using Skjermvette.Core;
using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Tests;

public class EnforcerTests
{
    private static readonly LogicalApp YouTube = new(Guid.NewGuid(), "YouTube", [new AppMatcher(MatcherKindEnum.Process, "youtube.exe")]);
    private static readonly LogicalApp Teams = new(Guid.NewGuid(), "Teams", [new AppMatcher(MatcherKindEnum.Process, "teams.exe")]);
    private static readonly UsageKey YouTubeKey = new(MatcherKindEnum.Process, "youtube.exe");
    private static readonly UsageKey TeamsKey = new(MatcherKindEnum.Process, "teams.exe");
    private static readonly UsageKey Notepad = new(MatcherKindEnum.Process, "notepad.exe");
    private static readonly UsageKey Explorer = new(MatcherKindEnum.Process, "explorer.exe");

    private readonly BudgetState _state = new(NullLogger<BudgetState>.Instance);
    private readonly List<string> _closed = [];
    private readonly Enforcer _enforcer;

    public EnforcerTests()
    {
        _state.SetCatalog([YouTube, Teams]);
        _enforcer = new Enforcer(_state, NullLogger<Enforcer>.Instance, program =>
        {
            _closed.Add(program);
            return 1;
        });
    }

    [Fact]
    public void BeforeTheFirstSync_NothingIsEnforced()
    {
        Assert.Empty(_enforcer.Check([YouTubeKey]));
        Assert.Empty(_closed);
    }

    [Fact]
    public void AnAppWhoseLimitIsUsedUp_IsClosed()
    {
        SetBudget(total: 60, youTube: 0);

        var notices = _enforcer.Check([YouTubeKey, Notepad]);

        Assert.Equal(["youtube.exe"], _closed);
        Assert.Equal([new HelperNotice(HelperNoticeEnum.Closed, "YouTube", 0)], notices);
    }

    [Fact]
    public void WhenTheTotalIsUsedUp_EverythingButExemptAppsAndWindowsIsClosed()
    {
        SetBudget(total: 0, youTube: 30);

        var notices = _enforcer.Check([YouTubeKey, Notepad, TeamsKey, Explorer]);

        Assert.Equal(["youtube.exe", "notepad.exe"], _closed);
        Assert.Equal([new HelperNotice(HelperNoticeEnum.Closed, null, 0)], notices);
    }

    [Fact]
    public void NothingClosed_NoNotice()
    {
        SetBudget(total: 0, youTube: 30);
        var enforcer = new Enforcer(_state, NullLogger<Enforcer>.Instance, _ => 0);

        Assert.Empty(enforcer.Check([Notepad]));
    }

    [Fact]
    public void Warnings_ComeOnceAt5AndAt1Minute()
    {
        SetBudget(total: 60, youTube: 4.5);
        Assert.Equal([new HelperNotice(HelperNoticeEnum.Warning, "YouTube", 5)], _enforcer.Check([YouTubeKey]));
        Assert.Empty(_enforcer.Check([YouTubeKey]));

        SetBudget(total: 60, youTube: 0.5);
        Assert.Equal([new HelperNotice(HelperNoticeEnum.Warning, "YouTube", 1)], _enforcer.Check([YouTubeKey]));
        Assert.Empty(_enforcer.Check([YouTubeKey]));
        Assert.Empty(_closed);
    }

    [Fact]
    public void AfterExtraTime_TheChildIsWarnedAgain()
    {
        SetBudget(total: 3, youTube: 30);
        Assert.Equal([new HelperNotice(HelperNoticeEnum.Warning, null, 3)], _enforcer.Check([Notepad, YouTubeKey]));

        SetBudget(total: 20, youTube: 30);
        Assert.Empty(_enforcer.Check([Notepad]));

        SetBudget(total: 2, youTube: 30);
        Assert.Equal([new HelperNotice(HelperNoticeEnum.Warning, null, 2)], _enforcer.Check([Notepad]));
    }

    [Fact]
    public void ExemptApps_AreNeverWarnedAboutOrClosed()
    {
        SetBudget(total: 0, youTube: 0);

        Assert.Empty(_enforcer.Check([TeamsKey]));
        Assert.Empty(_closed);
    }

    private void SetBudget(double total, double youTube) => _state.Set(ConnectionEnum.Online, new BudgetSnapshot(
        TimeSpan.FromMinutes(total), TimeSpan.Zero, TimeSpan.Zero,
        [new AppBudget(YouTube.Id, TimeSpan.FromMinutes(youTube), TimeSpan.Zero, TimeSpan.Zero)], [Teams.Id]));
}
