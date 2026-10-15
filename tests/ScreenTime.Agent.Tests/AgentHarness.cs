using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenTime.Agent.Service;
using ScreenTime.Core.Models;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Tests;

namespace ScreenTime.Agent.Tests;

/// <summary>A paired agent talking to the real server in memory, with a shared test clock and a switch to go offline.</summary>
public sealed class AgentHarness : IAsyncDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("screentime-agent-test-").FullName;
    private readonly Network _network;

    private AgentHarness(ServerFactory server, HttpClient parent, DeviceIdentity identity)
    {
        Server = server;
        Parent = parent;
        Identity = identity;
        _network = new Network(server);
        Sync = NewSync();
    }

    public ServerFactory Server { get; }
    public HttpClient Parent { get; }
    public DeviceIdentity Identity { get; }
    public TestClock Clock => Server.Clock;
    public UsageTracker Tracker { get; } = new();
    public BudgetState State { get; } = new(NullLogger<BudgetState>.Instance);
    public DeviceSync Sync { get; private set; }

    public bool Offline
    {
        get => _network.Offline;
        set => _network.Offline = value;
    }

    public static async Task<AgentHarness> StartAsync(ChildRequest? child = null)
    {
        var server = new ServerFactory();
        var parent = await server.CreateParentAsync();
        var created = await ServerFactory.CreateChildAsync(parent, child);
        var (_, pairing) = await server.PairDeviceAsync(parent, created.Id, "PC");
        return new AgentHarness(server, parent, new DeviceIdentity(server.Server.BaseAddress, pairing.DeviceId, pairing.ChildId, pairing.Token));
    }

    /// <summary>Samples <paramref name="key"/> in use once a second for <paramref name="duration"/>, moving the clock on.</summary>
    public void Use(UsageKey key, TimeSpan duration)
    {
        var end = Clock.Now + duration;
        for (; Clock.Now < end; Clock.Advance(TimeSpan.FromSeconds(1)))
            Tracker.Observe(Clock.Now, [key]);
        Tracker.Observe(Clock.Now, [key]);
    }

    /// <summary>Like a service restart: a new sync loop on the same data folder.</summary>
    public async Task RestartAsync()
    {
        await Sync.DisposeAsync();
        Sync = NewSync();
    }

    public async Task UnpairAsync() =>
        (await Parent.DeleteAsync($"/api/devices/{Identity.DeviceId}")).EnsureSuccessStatusCode();

    /// <summary>Waits until the budget matches <paramref name="condition"/>, e.g. after a push.</summary>
    public async Task<BudgetSnapshot> WaitForBudgetAsync(Func<BudgetSnapshot, bool> condition)
    {
        for (var waited = 0; ; waited += 50)
        {
            if (State.Snapshot is { } snapshot && condition(snapshot))
                return snapshot;
            Assert.True(waited < 10_000, "The budget never changed as expected.");
            await Task.Delay(50);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Sync.DisposeAsync();
        await Server.DisposeAsync();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A virus scanner can hold a new file for a moment; it's a temp folder.
        }
    }

    private DeviceSync NewSync() => new(Identity, _folder, Tracker, State, Clock, NullLogger.Instance, _network.CreateHandler);

    /// <summary>Connects to the in-memory server; while <see cref="Offline"/> is on, requests fail like an unreachable server.</summary>
    private sealed class Network(ServerFactory server)
    {
        public bool Offline { get; set; }

        public HttpMessageHandler CreateHandler() => new Switch(this, server.Server.CreateHandler());

        private sealed class Switch(Network network, HttpMessageHandler inner) : DelegatingHandler(inner)
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
                network.Offline ? throw new HttpRequestException("No connection (test).") : base.SendAsync(request, ct);
        }
    }
}
