using ScreenTime.Agent.Helper;
using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Tests;

public class SamplerTests
{
    private const string Chrome = @"C:\Program Files\Google\Chrome\Application\Chrome.exe";

    [Fact]
    public void ForegroundProgram_IsInUse_ByLowercaseProcessName()
    {
        var key = Assert.Single(Sampler.InUse(Chrome, TimeSpan.FromSeconds(3), sessionActive: true));

        Assert.Equal(new UsageKey(MatcherKindEnum.Process, "chrome.exe"), key);
    }

    [Fact]
    public void NoInputForTwoMinutes_MeansNothingInUse()
    {
        Assert.Single(Sampler.InUse(Chrome, Sampler.IdleAfter - TimeSpan.FromSeconds(1), sessionActive: true));
        Assert.Empty(Sampler.InUse(Chrome, Sampler.IdleAfter, sessionActive: true));
    }

    [Fact]
    public void LockedSession_MeansNothingInUse()
    {
        Assert.Empty(Sampler.InUse(Chrome, TimeSpan.Zero, sessionActive: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoForegroundProgram_MeansNothingInUse(string? path)
    {
        Assert.Empty(Sampler.InUse(path, TimeSpan.Zero, sessionActive: true));
    }

    [Fact]
    public void Elapsed_AllowsForTheTickCountWrapping()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Sampler.Elapsed(1_000, 6_000));
        Assert.Equal(TimeSpan.FromSeconds(5), Sampler.Elapsed(uint.MaxValue - 1_999, 3_000));
    }
}
