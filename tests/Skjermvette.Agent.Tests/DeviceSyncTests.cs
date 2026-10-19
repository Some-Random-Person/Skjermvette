using Skjermvette.Agent.Service;
using Skjermvette.Core.Models;
using Skjermvette.Server.Data;
using Skjermvette.Server.Tests;

namespace Skjermvette.Agent.Tests;

public class DeviceSyncTests
{
    private static readonly UsageKey YouTube = new(MatcherKindEnum.Domain, "youtube.com");
    private static readonly UsageKey Teams = new(MatcherKindEnum.Process, "ms-teams.exe");

    [Fact]
    public async Task Usage_IsUploaded_AndTheBudgetComesBack()
    {
        await using var agent = await AgentHarness.StartAsync();
        agent.Use(YouTube, TimeSpan.FromMinutes(1));

        await agent.Sync.TickAsync(CancellationToken.None);
        await agent.Sync.TickAsync(CancellationToken.None);

        Assert.Equal(ConnectionEnum.Online, agent.State.Connection);
        Assert.Equal(TimeSpan.FromMinutes(1), agent.State.Snapshot!.TotalUsed);
        Assert.Equal(TimeSpan.FromMinutes(29), agent.State.Snapshot.Remaining(SeedCatalog.YouTube));
    }

    [Fact]
    public async Task Offline_CountsDownTheAllowance_ThenBlocksAllButExemptApps()
    {
        await using var agent = await AgentHarness.StartAsync(ServerFactory.DefaultChild(offlineAllowanceMinutes: 5));
        await agent.Sync.TickAsync(CancellationToken.None);
        agent.Offline = true;

        agent.Use(YouTube, TimeSpan.FromMinutes(3));
        await agent.Sync.TickAsync(CancellationToken.None);
        Assert.Equal(ConnectionEnum.Offline, agent.State.Connection);
        Assert.Equal(TimeSpan.FromMinutes(2), agent.State.Snapshot!.Remaining(SeedCatalog.YouTube));

        agent.Use(YouTube, TimeSpan.FromMinutes(2));
        await agent.Sync.TickAsync(CancellationToken.None);
        Assert.True(agent.State.Snapshot!.IsBlocked(SeedCatalog.YouTube));
        Assert.True(agent.State.Snapshot.IsBlocked(null));
        Assert.False(agent.State.Snapshot.IsBlocked(SeedCatalog.Teams));
    }

    [Fact]
    public async Task BackOnline_UploadsOfflineUsage()
    {
        await using var agent = await AgentHarness.StartAsync();
        await agent.Sync.TickAsync(CancellationToken.None);
        agent.Offline = true;
        agent.Use(YouTube, TimeSpan.FromMinutes(2));
        await agent.Sync.TickAsync(CancellationToken.None);

        agent.Offline = false;
        await agent.Sync.TickAsync(CancellationToken.None);

        Assert.Equal(ConnectionEnum.Online, agent.State.Connection);
        Assert.Equal(TimeSpan.FromMinutes(2), agent.State.Snapshot!.TotalUsed);
    }

    [Fact]
    public async Task ExemptApps_DontUseTheAllowance()
    {
        await using var agent = await AgentHarness.StartAsync();
        await agent.Sync.TickAsync(CancellationToken.None);
        agent.Offline = true;

        agent.Use(Teams, TimeSpan.FromMinutes(10));
        await agent.Sync.TickAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(5), agent.State.Snapshot!.TotalRemaining);
    }

    [Fact]
    public async Task AllowanceOfZero_BlocksAsSoonAsOffline()
    {
        await using var agent = await AgentHarness.StartAsync(ServerFactory.DefaultChild(offlineAllowanceMinutes: 0));
        await agent.Sync.TickAsync(CancellationToken.None);
        agent.Offline = true;

        await agent.Sync.TickAsync(CancellationToken.None);

        Assert.True(agent.State.Snapshot!.IsBlocked(SeedCatalog.YouTube));
        Assert.True(agent.State.Snapshot.IsBlocked(null));
        Assert.False(agent.State.Snapshot.IsBlocked(SeedCatalog.Teams));
    }

    [Fact]
    public async Task OfflineWithoutEverSyncing_BlocksNothing()
    {
        await using var agent = await AgentHarness.StartAsync();
        agent.Offline = true;

        await agent.Sync.TickAsync(CancellationToken.None);

        Assert.Equal(ConnectionEnum.Offline, agent.State.Connection);
        Assert.Null(agent.State.Snapshot);
    }

    [Fact]
    public async Task Restart_WhileOffline_KeepsTheAllowanceAndUnsentUsage()
    {
        await using var agent = await AgentHarness.StartAsync();
        await agent.Sync.TickAsync(CancellationToken.None);
        agent.Offline = true;
        agent.Use(YouTube, TimeSpan.FromMinutes(1));
        await agent.Sync.SaveUsageAsync(CancellationToken.None);

        await agent.RestartAsync();
        await agent.Sync.TickAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(4), agent.State.Snapshot!.Remaining(SeedCatalog.YouTube));
    }

    [Fact]
    public async Task Allowance_ExpiresAtTheEndOfTheDay()
    {
        await using var agent = await AgentHarness.StartAsync();
        await agent.Sync.TickAsync(CancellationToken.None);
        agent.Offline = true;

        // The test child resets at midnight UTC; the clock starts at 10:00.
        agent.Clock.Advance(TimeSpan.FromHours(14));
        await agent.Sync.TickAsync(CancellationToken.None);

        Assert.True(agent.State.Snapshot!.IsBlocked(null));
    }

    [Fact]
    public async Task UsageOnAnotherDevice_IsPushedRightAway()
    {
        await using var agent = await AgentHarness.StartAsync();
        await agent.Sync.TickAsync(CancellationToken.None);
        var (phone, _) = await agent.Server.PairDeviceAsync(agent.Parent, agent.Identity.ChildId, "Phone");

        await agent.Server.UseAsync(phone, MatcherKindEnum.Domain, "youtube.com", 10);

        await agent.WaitForBudgetAsync(b => b.TotalUsed == TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Unpaired_Stops()
    {
        await using var agent = await AgentHarness.StartAsync();
        await agent.Sync.TickAsync(CancellationToken.None);

        await agent.UnpairAsync();

        await Assert.ThrowsAsync<UnpairedException>(() => agent.Sync.TickAsync(CancellationToken.None));
        Assert.Equal(ConnectionEnum.Unpaired, agent.State.Connection);
        Assert.Null(agent.State.Snapshot);
    }
}
