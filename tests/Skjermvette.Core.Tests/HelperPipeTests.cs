using System.Text;
using Skjermvette.Core.Models;

namespace Skjermvette.Core.Tests;

public class HelperPipeTests
{
    [Fact]
    public void Sample_RoundTrips()
    {
        UsageKey[] inUse = [new(MatcherKindEnum.Process, "chrome.exe"), new(MatcherKindEnum.Domain, "youtube.com")];

        var sample = HelperPipe.Decode(HelperPipe.Encode(new HelperSample(inUse, ["chrome.exe"])))!;

        Assert.Equal(inUse, sample.InUse);
        Assert.Equal(["chrome.exe"], sample.Browsers);
    }

    [Fact]
    public void EmptySample_MeansNothingInUse()
    {
        Assert.Empty(HelperPipe.Decode(HelperPipe.Encode(new HelperSample([], [])))!.InUse);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"inUse":[]}""")]
    [InlineData("""{"inUse":[null],"browsers":[]}""")]
    [InlineData("""{"inUse":[{"kind":"Process","identifier":""}],"browsers":[]}""")]
    [InlineData("""{"inUse":[{"kind":"Process"}],"browsers":[]}""")]
    [InlineData("""{"inUse":[{"kind":"Nonsense","identifier":"chrome.exe"}],"browsers":[]}""")]
    [InlineData("""{"inUse":[{"kind":99,"identifier":"chrome.exe"}],"browsers":[]}""")]
    [InlineData("""{"inUse":[],"browsers":[""]}""")]
    public void InvalidSamples_AreRefused(string message)
    {
        Assert.Null(HelperPipe.Decode(Encoding.UTF8.GetBytes(message)));
    }

    [Fact]
    public void Notice_RoundTripsOnOneLine()
    {
        var message = new HelperMessage(new HelperNotice(HelperNoticeEnum.Warning, "You\nTube", 5));

        var line = Encoding.UTF8.GetString(HelperPipe.EncodeMessage(message));

        Assert.Equal(line.Length - 1, line.IndexOf('\n'));
        Assert.Equal(message, HelperPipe.DecodeMessage(line.TrimEnd('\n')));
    }

    [Fact]
    public void Websites_RoundTrip()
    {
        var websites = new WebsiteBlocks(true, [new BlockedWebsite("youtube.com", "YouTube")], ["teams.microsoft.com"]);

        var decoded = HelperPipe.DecodeMessage(Encoding.UTF8.GetString(HelperPipe.EncodeMessage(new HelperMessage(Websites: websites))));

        Assert.True(decoded!.Websites!.BlockOthers);
        Assert.Equal(websites.Blocked, decoded.Websites.Blocked);
        Assert.Equal(websites.Allowed, decoded.Websites.Allowed);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"notice":{"kind":99,"app":null,"minutes":0}}""")]
    [InlineData("""{"notice":{"kind":"Warning","app":null,"minutes":1},"websites":{"blockOthers":false,"blocked":[],"allowed":[]}}""")]
    [InlineData("""{"websites":{"blockOthers":false,"blocked":[null],"allowed":[]}}""")]
    [InlineData("""{"websites":{"blockOthers":false,"blocked":[]}}""")]
    public void InvalidMessages_AreIgnored(string line)
    {
        Assert.Null(HelperPipe.DecodeMessage(line));
    }

    [Fact]
    public void TooManyKeys_OrTooLongIdentifiers_AreRefused()
    {
        var tooMany = Enumerable.Range(0, HelperPipe.MaxKeys + 1).Select(i => new UsageKey(MatcherKindEnum.Process, $"{i}.exe")).ToArray();
        UsageKey[] tooLong = [new(MatcherKindEnum.Domain, new string('a', HelperPipe.MaxIdentifierLength + 1))];

        Assert.Null(HelperPipe.Decode(HelperPipe.Encode(new HelperSample(tooMany, []))));
        Assert.Null(HelperPipe.Decode(HelperPipe.Encode(new HelperSample(tooLong, []))));
    }
}
