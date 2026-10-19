using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Skjermvette.Agent.Helper;
using Skjermvette.Agent.Service;
using Skjermvette.Core;
using Skjermvette.Core.DeviceApi;
using Skjermvette.Core.Models;
using Skjermvette.Server.Tests;

namespace Skjermvette.Agent.Tests;

/// <summary>The Helper's <see cref="PipeSender"/> and the Service's <see cref="HelperPipeServer"/> over a real named pipe, as the signed-in user.</summary>
public sealed class HelperPipeServerTests : IAsyncLifetime
{
    private static readonly UsageKey Chrome = new(MatcherKindEnum.Process, "chrome.exe");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _pipeName = $"Skjermvette.Agent.Test.{Guid.NewGuid():N}";
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly UsageTracker _tracker = new();
    private readonly BudgetState _state = new(NullLogger<BudgetState>.Instance);
    private readonly HelperProcess _helper = new(supervised: true);
    private readonly HelperPipeServer _server;

    public HelperPipeServerTests()
    {
        // The test's own process is the Helper the Service started.
        _helper.Started(Environment.ProcessId);
        _server = new HelperPipeServer(_tracker, new Enforcer(_state, NullLogger<Enforcer>.Instance, _ => 1), _helper, _clock,
            NullLogger<HelperPipeServer>.Instance, _pipeName);
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
    public async Task Notices_ReachTheHelper()
    {
        _state.Set(ConnectionEnum.Online, new BudgetSnapshot(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, [], []));
        using var sender = new PipeSender(_pipeName);

        HelperNotice? notice = null;
        var deadline = DateTime.UtcNow + Timeout;
        while (notice is null && DateTime.UtcNow < deadline)
        {
            sender.Send([Chrome]);
            await Task.Delay(100);
            if (sender.TryReceive(out var received))
                notice = received;
        }

        Assert.Equal(new HelperNotice(HelperNoticeEnum.Closed, null, 0), notice);
    }

    [Fact]
    public async Task SomethingThatIsntASample_DisconnectsTheClient()
    {
        Assert.True(await DisconnectedAsync(Encoding.UTF8.GetBytes("not a sample")));
        Assert.Empty(_tracker.Flush(_clock.Now));
    }

    [Fact]
    public async Task AProgramOtherThanTheHelper_IsDisconnected()
    {
        _helper.Started(Environment.ProcessId + 1);

        Assert.True(await DisconnectedAsync(HelperPipe.Encode([])));
        Assert.Empty(_tracker.Flush(_clock.Now));
    }

    [Fact]
    public async Task WithNoHelperRunning_NobodyIsTaken()
    {
        _helper.Stopped();

        Assert.True(await DisconnectedAsync(HelperPipe.Encode([Chrome])));
        Assert.Empty(_tracker.Flush(_clock.Now));
    }

    [Fact]
    public void NotSupervised_AnyClientIsTaken()
    {
        Assert.True(new HelperProcess(supervised: false).Accepts(Environment.ProcessId));
    }

    /// <summary>Keeps sending <paramref name="message"/> until the Service disconnects the client, or the time runs out.</summary>
    private async Task<bool> DisconnectedAsync(byte[] message)
    {
        await using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        await client.ConnectAsync((int)Timeout.TotalMilliseconds);

        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                _clock.Advance(TimeSpan.FromSeconds(1));
                await client.WriteAsync(message);
                await Task.Delay(100);
            }
            catch (IOException)
            {
                return true;
            }
        }
        return false;
    }
}
