using System.Text;
using Skjermvette.Core.Models;

namespace Skjermvette.Core.Tests;

public class HelperPipeTests
{
    [Fact]
    public void Sample_RoundTrips()
    {
        UsageKey[] inUse = [new(MatcherKindEnum.Process, "chrome.exe"), new(MatcherKindEnum.Domain, "youtube.com")];

        Assert.Equal(inUse, HelperPipe.Decode(HelperPipe.Encode(inUse)));
    }

    [Fact]
    public void EmptySample_MeansNothingInUse()
    {
        Assert.Empty(HelperPipe.Decode(HelperPipe.Encode([]))!);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("""[{"kind":"Process","identifier":""}]""")]
    [InlineData("""[{"kind":"Process"}]""")]
    [InlineData("""[{"kind":"Nonsense","identifier":"chrome.exe"}]""")]
    [InlineData("""[{"kind":99,"identifier":"chrome.exe"}]""")]
    public void InvalidSamples_AreRefused(string message)
    {
        Assert.Null(HelperPipe.Decode(Encoding.UTF8.GetBytes(message)));
    }

    [Fact]
    public void Notice_RoundTripsOnOneLine()
    {
        var notice = new HelperNotice(HelperNoticeEnum.Warning, "You\nTube", 5);

        var line = Encoding.UTF8.GetString(HelperPipe.EncodeNotice(notice));

        Assert.Equal(line.Length - 1, line.IndexOf('\n'));
        Assert.Equal(notice, HelperPipe.DecodeNotice(line.TrimEnd('\n')));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"kind":99,"app":null,"minutes":0}""")]
    public void InvalidNotices_AreIgnored(string line)
    {
        Assert.Null(HelperPipe.DecodeNotice(line));
    }

    [Fact]
    public void TooManyKeys_OrTooLongIdentifiers_AreRefused()
    {
        var tooMany = Enumerable.Range(0, HelperPipe.MaxKeys + 1).Select(i => new UsageKey(MatcherKindEnum.Process, $"{i}.exe")).ToArray();
        UsageKey[] tooLong = [new(MatcherKindEnum.Domain, new string('a', HelperPipe.MaxIdentifierLength + 1))];

        Assert.Null(HelperPipe.Decode(HelperPipe.Encode(tooMany)));
        Assert.Null(HelperPipe.Decode(HelperPipe.Encode(tooLong)));
    }
}
