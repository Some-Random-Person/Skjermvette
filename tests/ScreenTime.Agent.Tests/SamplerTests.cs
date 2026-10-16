using ScreenTime.Agent.Helper;
using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Tests;

public class SamplerTests
{
    private const string Chrome = @"C:\Program Files\Google\Chrome\Application\Chrome.exe";
    private const string Spotify = @"C:\Users\kid\AppData\Roaming\Spotify\Spotify.exe";
    private static readonly UsageKey ChromeKey = new(MatcherKindEnum.Process, "chrome.exe");
    private static readonly UsageKey SpotifyKey = new(MatcherKindEnum.Process, "spotify.exe");

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
    public void Elapsed_AllowsForTheTickCountWrapping()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Sampler.Elapsed(1_000, 6_000));
        Assert.Equal(TimeSpan.FromSeconds(5), Sampler.Elapsed(uint.MaxValue - 1_999, 3_000));
    }
}
