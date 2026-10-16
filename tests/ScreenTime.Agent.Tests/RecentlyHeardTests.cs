using ScreenTime.Agent.Helper;

namespace ScreenTime.Agent.Tests;

public class RecentlyHeardTests
{
    private const string Spotify = "spotify.exe";

    [Fact]
    public void Program_StaysHeard_ForAShortPause_ThenStops()
    {
        var heard = new RecentlyHeard();
        var start = TimeSpan.FromHours(1);

        Assert.Equal([Spotify], heard.Update(start, [Spotify]));
        Assert.Equal([Spotify], heard.Update(start + RecentlyHeard.HoldFor, []));
        Assert.Empty(heard.Update(start + RecentlyHeard.HoldFor + TimeSpan.FromSeconds(1), []));
    }

    [Fact]
    public void HearingAgain_StartsThePauseOver()
    {
        var heard = new RecentlyHeard();

        heard.Update(TimeSpan.FromSeconds(0), [Spotify]);
        heard.Update(TimeSpan.FromSeconds(4), [Spotify]);

        Assert.Equal([Spotify], heard.Update(TimeSpan.FromSeconds(9), []));
    }

    [Fact]
    public void NothingHeard_MeansNothing()
    {
        Assert.Empty(new RecentlyHeard().Update(TimeSpan.Zero, []));
    }
}
