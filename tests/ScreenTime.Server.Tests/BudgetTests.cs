using System.Net;
using System.Net.Http.Json;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

public class BudgetTests
{
    private static readonly TimeSpan Min = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task YouTubeInBrowser_CountsAsYouTube_AndIsBlockedAt30Minutes()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);

        var sync = await factory.UseAsync(pc, MatcherKindEnum.Domain, "www.youtube.com", 30);

        Assert.True(sync.Snapshot.IsBlocked(SeedCatalog.YouTube));
        Assert.Equal(90 * Min, sync.Snapshot.TotalRemaining);
    }

    [Fact]
    public async Task ExemptTeams_DoesNotReduceTheTotal()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);

        var sync = await factory.UseAsync(pc, MatcherKindEnum.Process, @"C:\Program Files\Teams\ms-teams.exe", 30);

        Assert.Equal(120 * Min, sync.Snapshot.TotalRemaining);
        Assert.False(sync.Snapshot.IsBlocked(SeedCatalog.Teams));
    }

    [Fact]
    public async Task UsageOnThePhone_LimitsThePc()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id, "PC");
        var (phone, _) = await factory.PairDeviceAsync(parent, child.Id, "Phone");

        await factory.UseAsync(phone, MatcherKindEnum.Package, "com.google.android.youtube", 15);
        var pcState = await StateAsync(pc);

        // 30 − 15 used on the phone − 5 set aside as the phone's offline allowance.
        Assert.Equal(10 * Min, pcState.Snapshot.Remaining(SeedCatalog.YouTube));
        Assert.Equal(5 * Min, pcState.Snapshot.TotalReserved);
    }

    [Fact]
    public async Task UsingThePhoneAndThePcAtTheSameTime_CountsOnce()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id, "PC");
        var (phone, _) = await factory.PairDeviceAsync(parent, child.Id, "Phone");
        var start = factory.Clock.Now.AddMinutes(-30);

        await factory.UseAsync(phone, MatcherKindEnum.Package, "com.google.android.youtube", 10, start);
        await factory.UseAsync(pc, MatcherKindEnum.Domain, "youtube.com", 10, start);
        var today = await parent.GetFromJsonAsync<TodayResponse>($"/api/children/{child.Id}/today", Json);

        Assert.Equal(10, today!.TotalUsedMinutes, 3);
        Assert.Contains(today.Apps, a => a.AppId == SeedCatalog.YouTube && Math.Abs(a.UsedMinutes - 10) < 0.001);
    }

    [Fact]
    public async Task ResendingTheSameEvents_DoesNotCountThemTwice()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);
        var batch = new UsageBatchRequest([new UsageEventDto(Guid.NewGuid(), MatcherKindEnum.Process, "notepad.exe", factory.Clock.Now.AddMinutes(-10), 300)]);

        await pc.PostAsJsonAsync("/api/device/usage", batch, Json);
        var second = await (await pc.PostAsJsonAsync("/api/device/usage", batch, Json)).Content.ReadFromJsonAsync<SyncResponse>(Json);

        Assert.Equal(5 * Min, second!.Snapshot.TotalUsed);
    }

    [Fact]
    public async Task Sync_GrantsTheChildsOfflineAllowance()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, pairing) = await factory.PairDeviceAsync(parent, child.Id);

        var sync = await (await pc.PostAsync("/api/device/lease", null)).Content.ReadFromJsonAsync<SyncResponse>(Json);

        Assert.Equal(pairing.DeviceId, sync!.Lease.DeviceId);
        Assert.Equal(5 * Min, sync.Lease.Total);
        Assert.Equal(5 * Min, sync.Lease.Apps[SeedCatalog.YouTube]);
        Assert.False(sync.Lease.Apps.ContainsKey(SeedCatalog.Teams));
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero), sync.Lease.ExpiresAt);
    }

    [Fact]
    public async Task DeviceOverride_BeatsTheChildDefault_FromTheNextRenewal()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, pairing) = await factory.PairDeviceAsync(parent, child.Id);
        var before = await (await pc.PostAsync("/api/device/lease", null)).Content.ReadFromJsonAsync<SyncResponse>(Json);

        (await parent.PutAsJsonAsync($"/api/devices/{pairing.DeviceId}", new DeviceUpdateRequest("PC", 2), Json)).EnsureSuccessStatusCode();
        var after = await (await pc.PostAsync("/api/device/lease", null)).Content.ReadFromJsonAsync<SyncResponse>(Json);

        Assert.Equal(5 * Min, before!.Lease.Total);
        Assert.Equal(2 * Min, after!.Lease.Total);
    }

    [Fact]
    public async Task ZeroAllowance_GrantsNothing_SoTheDeviceLocksWhenOffline()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent, DefaultChild(offlineAllowanceMinutes: 0));
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);

        var sync = await (await pc.PostAsync("/api/device/lease", null)).Content.ReadFromJsonAsync<SyncResponse>(Json);
        var offline = Core.BudgetCalculator.CalculateOffline(sync!.Lease, new HashSet<Guid> { SeedCatalog.Teams }, []);

        Assert.Equal(TimeSpan.Zero, sync.Lease.Total);
        Assert.True(offline.IsBlocked(null));
        Assert.False(offline.IsBlocked(SeedCatalog.Teams));
    }

    [Fact]
    public async Task EventInTheFuture_IsRejected()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);
        var batch = new UsageBatchRequest([new UsageEventDto(Guid.NewGuid(), MatcherKindEnum.Process, "notepad.exe", factory.Clock.Now.AddHours(1), 60)]);

        var response = await pc.PostAsJsonAsync("/api/device/usage", batch, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EventLongerThanAnHour_IsRejected()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);
        var batch = new UsageBatchRequest([new UsageEventDto(Guid.NewGuid(), MatcherKindEnum.Process, "notepad.exe", factory.Clock.Now.AddHours(-3), 7200)]);

        var response = await pc.PostAsJsonAsync("/api/device/usage", batch, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
