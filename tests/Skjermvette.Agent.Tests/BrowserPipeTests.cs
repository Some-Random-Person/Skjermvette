using System.Text;
using Skjermvette.Agent.Helper;

namespace Skjermvette.Agent.Tests;

public class BrowserPipeTests
{
    [Fact]
    public void Decode_ReadsTheExtensionsReport()
    {
        var report = BrowserPipe.Decode("""{"browser":"msedge.exe","active":"www.youtube.com","audible":["music.youtube.com"]}"""u8);

        Assert.NotNull(report);
        Assert.Equal(("msedge.exe", "www.youtube.com"), (report.Browser, report.Active));
        Assert.Equal(["music.youtube.com"], report.Audible);
    }

    [Fact]
    public void Decode_TakesNoActiveWebsite()
    {
        var report = BrowserPipe.Decode("""{"browser":"chrome.exe","active":null,"audible":[]}"""u8);

        Assert.NotNull(report);
        Assert.Null(report.Active);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"active":null,"audible":[]}""")]
    [InlineData("""{"browser":"chrome.exe","active":null}""")]
    [InlineData("""{"browser":"Chrome","active":null,"audible":[]}""")]
    [InlineData("""{"browser":"chrome.exe","active":"YouTube.com","audible":[]}""")]
    [InlineData("""{"browser":"chrome.exe","active":"https://youtube.com/","audible":[]}""")]
    [InlineData("""{"browser":"chrome.exe","active":null,"audible":[null]}""")]
    [InlineData("""{"browser":"chrome.exe","active":null,"audible":["[::1]"]}""")]
    public void Decode_RefusesAnythingElse(string message)
    {
        Assert.Null(BrowserPipe.Decode(Encoding.UTF8.GetBytes(message)));
    }

    [Fact]
    public void Decode_RefusesTooManyTabs()
    {
        var audible = string.Join(",", Enumerable.Range(0, BrowserPipe.MaxAudible + 1).Select(i => $"\"site{i}.com\""));

        Assert.Null(BrowserPipe.Decode(Encoding.UTF8.GetBytes($$"""{"browser":"chrome.exe","active":null,"audible":[{{audible}}]}""")));
    }
}
