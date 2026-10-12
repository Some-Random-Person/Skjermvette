using ScreenTime.Core.Models;

namespace ScreenTime.Core.Tests;

public class AppResolverTests
{
    private static readonly LogicalApp YouTube = new(Guid.NewGuid(), "YouTube",
    [
        new(MatcherKindEnum.Domain, "youtube.com"),
        new(MatcherKindEnum.Domain, "youtu.be"),
        new(MatcherKindEnum.Package, "com.google.android.youtube"),
    ]);

    private static readonly LogicalApp YouTubeMusic = new(Guid.NewGuid(), "YouTube Music",
        [new(MatcherKindEnum.Domain, "music.youtube.com")]);

    private static readonly LogicalApp Teams = new(Guid.NewGuid(), "Teams",
    [
        new(MatcherKindEnum.Process, "ms-teams.exe"),
        new(MatcherKindEnum.IosToken, "AbC123=="),
    ]);

    private readonly AppResolver _resolver = new([YouTube, YouTubeMusic, Teams]);

    [Theory]
    [InlineData("youtube.com")]
    [InlineData("www.youtube.com")]
    [InlineData("m.YouTube.com")]
    [InlineData("youtu.be")]
    [InlineData("youtube.com.")]
    public void Domain_MatchesItselfAndSubdomains(string host)
    {
        Assert.Equal(YouTube, _resolver.Resolve(MatcherKindEnum.Domain, host));
    }

    [Fact]
    public void Domain_MostSpecificMatchWins()
    {
        Assert.Equal(YouTubeMusic, _resolver.Resolve(MatcherKindEnum.Domain, "music.youtube.com"));
    }

    [Theory]
    [InlineData("notyoutube.com")]
    [InlineData("youtube.co")]
    [InlineData("com")]
    public void Domain_DoesNotMatchLookalikes(string host)
    {
        Assert.Null(_resolver.Resolve(MatcherKindEnum.Domain, host));
    }

    [Theory]
    [InlineData("ms-teams.exe")]
    [InlineData("MS-Teams.EXE")]
    [InlineData(@"C:\Program Files\WindowsApps\MSTeams\ms-teams.exe")]
    [InlineData("/opt/teams/ms-teams.exe")]
    public void Process_MatchesByFileNameIgnoringCase(string raw)
    {
        Assert.Equal(Teams, _resolver.Resolve(MatcherKindEnum.Process, raw));
    }

    [Fact]
    public void Package_MatchesExactly()
    {
        Assert.Equal(YouTube, _resolver.Resolve(MatcherKindEnum.Package, "com.google.android.youtube"));
        Assert.Null(_resolver.Resolve(MatcherKindEnum.Package, "com.google.android.youtube.tv"));
    }

    [Fact]
    public void IosToken_IsCaseSensitive()
    {
        Assert.Equal(Teams, _resolver.Resolve(MatcherKindEnum.IosToken, "AbC123=="));
        Assert.Null(_resolver.Resolve(MatcherKindEnum.IosToken, "abc123=="));
    }

    [Fact]
    public void Kinds_DoNotCrossMatch()
    {
        Assert.Null(_resolver.Resolve(MatcherKindEnum.Process, "youtube.com"));
    }
}
