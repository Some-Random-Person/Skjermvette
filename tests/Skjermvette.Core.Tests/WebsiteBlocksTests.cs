using Skjermvette.Core.Models;
using static Skjermvette.Core.Tests.Fixtures;

namespace Skjermvette.Core.Tests;

public class WebsiteBlocksTests
{
    private static readonly Guid YouTubeMusic = Guid.Parse("00000000-0000-0000-0000-0000000000a4");

    private static readonly LogicalApp[] Catalog =
    [
        new(YouTube, "YouTube", [new AppMatcher(MatcherKindEnum.Domain, "YouTube.com."), new AppMatcher(MatcherKindEnum.Process, "youtube.exe")]),
        new(YouTubeMusic, "YouTube Music", [new AppMatcher(MatcherKindEnum.Domain, "music.youtube.com")]),
        new(Teams, "Teams", [new AppMatcher(MatcherKindEnum.Domain, "teams.microsoft.com"), new AppMatcher(MatcherKindEnum.Domain, "*.teams.live.com")]),
    ];

    [Fact]
    public void WithoutABudget_NothingIsBlocked()
    {
        Assert.Same(WebsiteBlocks.Nothing, WebsiteBlocks.For(null, Catalog, "skjermvette.example.com"));
    }

    [Fact]
    public void AnAppWhoseLimitIsUsedUp_IsBlockedByName_ButNotItsMoreSpecificNeighbour()
    {
        var blocks = WebsiteBlocks.For(Budget(total: 60, youTube: 0), Catalog, "skjermvette.example.com");

        Assert.False(blocks.BlockOthers);
        Assert.Equal([new BlockedWebsite("youtube.com", "YouTube")], blocks.Blocked);
        Assert.Equal(["music.youtube.com", "teams.microsoft.com", "skjermvette.example.com"], blocks.Allowed);
    }

    [Fact]
    public void WhenTheTotalIsUsedUp_EverythingButExemptAppsAndTheDashboardIsBlocked()
    {
        var blocks = WebsiteBlocks.For(Budget(total: 0, youTube: 0), Catalog, "skjermvette.example.com");

        Assert.True(blocks.BlockOthers);
        Assert.Equal([new BlockedWebsite("youtube.com", null), new BlockedWebsite("music.youtube.com", null)], blocks.Blocked);
        Assert.Equal(["teams.microsoft.com", "skjermvette.example.com"], blocks.Allowed);
    }

    [Fact]
    public void WithTimeLeft_NothingIsBlocked()
    {
        var blocks = WebsiteBlocks.For(Budget(total: 60, youTube: 10), Catalog, null);

        Assert.False(blocks.BlockOthers);
        Assert.Empty(blocks.Blocked);
    }

    private static BudgetSnapshot Budget(double total, double youTube) => new(
        Min(total), TimeSpan.Zero, TimeSpan.Zero, [new AppBudget(YouTube, Min(youTube), TimeSpan.Zero, TimeSpan.Zero)], [Teams]);
}
