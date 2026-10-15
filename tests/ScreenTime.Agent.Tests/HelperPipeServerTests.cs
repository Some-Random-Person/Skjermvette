using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenTime.Agent.Helper;
using ScreenTime.Agent.Service;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;
using ScreenTime.Server.Tests;

namespace ScreenTime.Agent.Tests;

/// <summary>The Helper's <see cref="PipeSender"/> and the Service's <see cref="HelperPipeServer"/> over a real named pipe, as the signed-in user.</summary>
public sealed class HelperPipeServerTests : IAsyncLifetime
{
    private static readonly UsageKey Chrome = new(MatcherKindEnum.Process, "chrome.exe");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _pipeName = $"ScreenTime.Agent.Test.{Guid.NewGuid():N}";
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly UsageTracker _tracker = new();
    private readonly HelperPipeServer _server;

    public HelperPipeServerTests()
    {
        _server = new HelperPipeServer(_tracker, _clock, NullLogger<HelperPipeServer>.Instance, _pipeName);
    }

    public Task InitializeAsync() => _server.StartAsync(CancellationToken.None);

    public async Task DisposeAsync()
    {
        await _server.StopAsync(CancellationToken.None);
        _server.Dispose();
    }

    [Fact]
    public async Task SamplesFromTheHelper_AreCounted()
    {
        using var sender = new PipeSender(_pipeName);

        var events = new List<UsageEventDto>();
        var deadline = DateTime.UtcNow + Timeout;
        while (events.Count == 0 && DateTime.UtcNow < deadline)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            sender.Send([Chrome]);
            await Task.Delay(100);
            events.AddRange(_tracker.Flush(_clock.Now));
        }

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Equal(("chrome.exe", MatcherKindEnum.Process), (e.RawIdentifier, e.Kind)));
    }

    [Fact]
    public async Task SomethingThatIsntASample_DisconnectsTheClient()
    {
        await using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        await client.ConnectAsync((int)Timeout.TotalMilliseconds);

        var disconnected = false;
        var deadline = DateTime.UtcNow + Timeout;
        while (!disconnected && DateTime.UtcNow < deadline)
        {
            try
            {
                await client.WriteAsync(Encoding.UTF8.GetBytes("not a sample"));
                await Task.Delay(100);
            }
            catch (IOException)
            {
                disconnected = true;
            }
        }

        Assert.True(disconnected);
        Assert.Empty(_tracker.Flush(_clock.Now));
    }
}
