using Skjermvette.Agent.Helper;
using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Tests;

public class SamplerTests
{
    private const string Chrome = @"C:\Program Files\Google\Chrome\Application\Chrome.exe";
    private const string Spotify = @"C:\Users\kid\AppData\Roaming\Spotify\Spotify.exe";
    private static readonly UsageKey ChromeKey = new(MatcherKindEnum.Process, "chrome.exe");
    private static readonly UsageKey SpotifyKey = new(MatcherKindEnum.Process, "spotify.exe");
    private static readonly UsageKey YouTube = new(MatcherKindEnum.Domain, "www.youtube.com");
    private static readonly UsageKey Wikipedia = new(MatcherKindEnum.Domain, "en.wikipedia.org");

    [Fact]
    public void ForegroundProgram_IsInUse_ByLowercaseProcessName()
    {
        var key = Assert.Single(Sampler.InUse(Chrome, [], TimeSpan.FromSeconds(3), sessionActive: true));

        Assert.Equal(ChromeKey, key);
    }

    [Fact]
    public void NoInputForTwoMinutes_AndNoSound_MeansNothingInUse()
    {
        Assert.Single(Sampler.InUse(Chrome, [], Sampler.IdleAfter - TimeSpan.FromSeconds(1), sessionActive: true));
        Assert.Empty(Sampler.InUse(Chrome, [], Sampler.IdleAfter, sessionActive: true));
    }

    [Fact]
    public void ProgramMakingSound_IsInUse_AsWellAsTheOneInFront()
    {
        Assert.Equal([ChromeKey, SpotifyKey], Sampler.InUse(Chrome, [Spotify], TimeSpan.Zero, sessionActive: true).OrderBy(k => k.Identifier));
    }

    [Fact]
    public void ProgramInFront_MakingSound_IsReportedOnce()
    {
        Assert.Equal([ChromeKey], Sampler.InUse(Chrome, [Chrome], TimeSpan.Zero, sessionActive: true));
    }

    [Fact]
    public void NoInputForTwoMinutes_CountsOnlySound()
    {
        Assert.Equal([SpotifyKey], Sampler.InUse(Chrome, [Spotify], Sampler.IdleAfter, sessionActive: true));
        Assert.Equal([ChromeKey], Sampler.InUse(Chrome, [Chrome], Sampler.IdleAfter, sessionActive: true));
    }

    [Fact]
    public void LockedSession_CountsOnlySound()
    {
        Assert.Empty(Sampler.InUse(Chrome, [], TimeSpan.Zero, sessionActive: false));
        Assert.Equal([SpotifyKey], Sampler.InUse(Chrome, [Spotify], TimeSpan.Zero, sessionActive: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoForegroundProgram_MeansNothingInUse(string? path)
    {
        Assert.Empty(Sampler.InUse(path, [], TimeSpan.Zero, sessionActive: true));
        Assert.Equal([SpotifyKey], Sampler.InUse(path, [Spotify, path], TimeSpan.Zero, sessionActive: true));
    }

    [Fact]
    public void BrowserInFront_CountsAsTheWebsiteInItsActiveTab()
    {
        BrowserReport[] reports = [new("chrome.exe", "www.youtube.com", [])];

        Assert.Equal([YouTube], Sampler.InUse(Chrome, [], TimeSpan.Zero, sessionActive: true, reports));
    }

    [Fact]
    public void BrowserInFront_OnAPageThatIsntAWebsite_OrWithoutAReport_CountsAsItself()
    {
        Assert.Equal([ChromeKey], Sampler.InUse(Chrome, [], TimeSpan.Zero, sessionActive: true, [new("chrome.exe", null, [])]));
        Assert.Equal([ChromeKey], Sampler.InUse(Chrome, [], TimeSpan.Zero, sessionActive: true, [new("msedge.exe", "www.youtube.com", [])]));
    }

    [Fact]
    public void BrowserMakingSound_CountsAsTheWebsitesInItsTabsMakingSound()
    {
        BrowserReport[] reports = [new("chrome.exe", "en.wikipedia.org", ["www.youtube.com"])];

        Assert.Equal([Wikipedia, YouTube], Sampler.InUse(Chrome, [Chrome], TimeSpan.Zero, sessionActive: true, reports).OrderBy(k => k.Identifier));
        Assert.Equal([YouTube], Sampler.InUse(Chrome, [Chrome], Sampler.IdleAfter, sessionActive: true, reports));
    }

    [Fact]
    public void TabsMakingSound_CountOnlyWhileTheBrowserIsHeard()
    {
        BrowserReport[] reports = [new("chrome.exe", "en.wikipedia.org", ["www.youtube.com"])];

        Assert.Equal([Wikipedia], Sampler.InUse(Chrome, [], TimeSpan.Zero, sessionActive: true, reports));
    }

    [Fact]
    public void BrowserMakingSound_WithNoTabReportedAsMakingSound_CountsAsItself()
    {
        BrowserReport[] reports = [new("chrome.exe", null, [])];

        Assert.Equal([ChromeKey, SpotifyKey], Sampler.InUse(Spotify, [Chrome], TimeSpan.Zero, sessionActive: true, reports).OrderBy(k => k.Identifier));
    }

    [Fact]
    public void Elapsed_AllowsForTheTickCountWrapping()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Sampler.Elapsed(1_000, 6_000));
        Assert.Equal(TimeSpan.FromSeconds(5), Sampler.Elapsed(uint.MaxValue - 1_999, 3_000));
    }
}
