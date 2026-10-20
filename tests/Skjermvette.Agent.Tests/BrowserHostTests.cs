using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using Skjermvette.Agent.Helper;
using Skjermvette.Core;

namespace Skjermvette.Agent.Tests;

/// <summary>
/// The native messaging host (<see cref="BrowserHost"/>) passing the extension's messages to <see cref="BrowserPipeServer"/>
/// over a real pipe, and the websites to block back.
/// </summary>
public sealed class BrowserHostTests : IDisposable
{
    private const string Report = """{"browser":"chrome.exe","active":"www.youtube.com","audible":[]}""";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _pipeName = $"Skjermvette.Agent.Browser.Test.{Guid.NewGuid():N}";
    private readonly BrowserPipeServer _server;
    // Standard input as the browser holds it: reading waits until the browser writes or closes it.
    private readonly AnonymousPipeServerStream _browser = new(PipeDirection.Out);
    private readonly AnonymousPipeClientStream _input;
    // Standard output as the browser reads it.
    private readonly AnonymousPipeServerStream _fromHost = new(PipeDirection.In);
    private readonly AnonymousPipeClientStream _output;

    public BrowserHostTests()
    {
        _server = new BrowserPipeServer(_pipeName);
        _input = new AnonymousPipeClientStream(PipeDirection.In, _browser.ClientSafePipeHandle);
        _output = new AnonymousPipeClientStream(PipeDirection.Out, _fromHost.ClientSafePipeHandle);
    }

    public void Dispose()
    {
        _browser.Dispose();
        _input.Dispose();
        _fromHost.Dispose();
        _output.Dispose();
        _server.Dispose();
    }

    [Fact]
    public void StartedByBrowser_WhenTheFirstArgumentIsTheExtensionsOrigin()
    {
        Assert.True(BrowserHost.StartedByBrowser(["chrome-extension://abcdefghijklmnopabcdefghijklmnop/", "--parent-window=0"]));
        Assert.False(BrowserHost.StartedByBrowser([]));
        Assert.False(BrowserHost.StartedByBrowser(["--service"]));
    }

    [Fact]
    public async Task ReportsFromTheExtension_ReachTheHelper_UntilTheBrowserDisconnects()
    {
        var host = BrowserHost.RunAsync(_input, _output, _pipeName);

        _browser.Write(Framed(Report));
        var report = await WaitForAsync(() => _server.Reports().SingleOrDefault());
        Assert.Equal(("chrome.exe", "www.youtube.com"), (report.Browser, report.Active));

        _browser.Dispose();
        Assert.Equal(0, await host.WaitAsync(Timeout));
        await WaitForAsync(() => _server.Reports().Count == 0 ? "forgotten" : null);
    }

    [Fact]
    public async Task WebsitesToBlock_ReachTheExtension_WhenItConnectsAndWhenTheyChange()
    {
        _server.Block(new WebsiteBlocks(false, [new BlockedWebsite("youtube.com", "YouTube")], []));
        _ = BrowserHost.RunAsync(_input, _output, _pipeName);

        Assert.Equal("""{"blockOthers":false,"blocked":[{"domain":"youtube.com","app":"YouTube"}],"allowed":[]}""", await ReadFromHostAsync());

        _server.Block(WebsiteBlocks.Nothing);
        Assert.Equal("""{"blockOthers":false,"blocked":[],"allowed":[]}""", await ReadFromHostAsync());
    }

    [Fact]
    public async Task InvalidReport_ClosesTheConnection()
    {
        var host = BrowserHost.RunAsync(_input, _output, _pipeName);

        _browser.Write(Framed("""{"browser":"chrome.exe"}"""));
        // Once the Helper has closed the pipe, the host's next write fails.
        while (!host.IsCompleted)
        {
            _browser.Write(Framed(Report));
            await Task.WhenAny(host, Task.Delay(100));
        }

        Assert.Equal(1, await host);
        Assert.Empty(_server.Reports());
    }

    [Fact]
    public async Task TooLongAMessage_EndsTheHost()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, BrowserPipe.MaxMessageBytes + 1);

        Assert.Equal(1, await BrowserHost.RunAsync(new MemoryStream(header), Stream.Null, _pipeName).WaitAsync(Timeout));
    }

    [Fact]
    public async Task NoHelper_EndsTheHost()
    {
        var noHelper = $"Skjermvette.Agent.Browser.Test.{Guid.NewGuid():N}";

        Assert.Equal(1, await BrowserHost.RunAsync(new MemoryStream(Framed(Report)), Stream.Null, noHelper).WaitAsync(Timeout));
    }

    /// <summary>One message from the host, as the browser reads it: a 4-byte length, then JSON.</summary>
    private async Task<string> ReadFromHostAsync()
    {
        var header = new byte[4];
        await _fromHost.ReadExactlyAsync(header).AsTask().WaitAsync(Timeout);
        var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
        await _fromHost.ReadExactlyAsync(body).AsTask().WaitAsync(Timeout);
        return Encoding.UTF8.GetString(body);
    }

    private static byte[] Framed(string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var framed = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(framed, body.Length);
        body.CopyTo(framed, 4);
        return framed;
    }

    private static async Task<T> WaitForAsync<T>(Func<T?> get) where T : class
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (get() is { } value)
                return value;
            await Task.Delay(50);
        }
        throw new TimeoutException();
    }
}
