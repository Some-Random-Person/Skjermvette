using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Skjermvette.Core.DeviceApi;
using Skjermvette.Core.Models;
using Skjermvette.Server.Contracts;
using Skjermvette.Server.Data.Entities;
using static Skjermvette.Server.Tests.ServerFactory;

namespace Skjermvette.Server.Tests;

public class PairingTests
{
    [Fact]
    public async Task PairedDevice_CanReadItsState()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (device, pairing) = await factory.PairDeviceAsync(parent, child.Id);

        var state = await StateAsync(device);

        Assert.Equal(pairing.DeviceId, state.DeviceId);
        Assert.Equal(child.Id, state.ChildId);
        Assert.Equal(TimeSpan.FromHours(2), state.Policy.DailyTotal);
        Assert.Contains(state.Catalog, a => a.Name == "YouTube");
    }

    [Fact]
    public async Task PairingCode_WorksOnlyOnce()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var code = await (await parent.PostAsync($"/api/children/{child.Id}/pairing-codes", null)).Content.ReadFromJsonAsync<PairingCodeResponse>(Json);
        var request = new PairRequest(code!.Code, "PC", DevicePlatformEnum.Windows);

        var first = await factory.CreateClient().PostAsJsonAsync("/api/device/pair", request, Json);
        var second = await factory.CreateClient().PostAsJsonAsync("/api/device/pair", request, Json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task PairingCode_ExpiresAfterTenMinutes()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var code = await (await parent.PostAsync($"/api/children/{child.Id}/pairing-codes", null)).Content.ReadFromJsonAsync<PairingCodeResponse>(Json);

        factory.Clock.Advance(TimeSpan.FromMinutes(11));
        var response = await factory.CreateClient().PostAsJsonAsync("/api/device/pair", new PairRequest(code!.Code, "PC", DevicePlatformEnum.Windows), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownToken_Is401()
    {
        using var factory = new ServerFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/device/state")).StatusCode);
    }

    [Fact]
    public async Task UnpairedDevice_TokenStopsWorking()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (device, pairing) = await factory.PairDeviceAsync(parent, child.Id);

        (await parent.DeleteAsync($"/api/devices/{pairing.DeviceId}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/device/state")).StatusCode);
    }

    [Fact]
    public async Task UnpairedDevice_DisappearsFromTheDashboard_ButItsUsageStillCounts()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (phone, phonePairing) = await factory.PairDeviceAsync(parent, child.Id, "Phone");
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id, "PC");
        await factory.UseAsync(phone, MatcherKindEnum.Process, "notepad.exe", 20);

        (await parent.DeleteAsync($"/api/devices/{phonePairing.DeviceId}")).EnsureSuccessStatusCode();
        var devices = await parent.GetFromJsonAsync<List<DeviceResponse>>($"/api/devices?childId={child.Id}", Json);
        var today = await parent.GetFromJsonAsync<TodayResponse>($"/api/children/{child.Id}/today", Json);
        var pcState = await StateAsync(pc);

        Assert.Equal(["PC"], devices!.Select(d => d.Name));
        Assert.Equal(["PC"], today!.Devices.Select(d => d.Name));
        Assert.Equal(20, today.TotalUsedMinutes, 3);
        Assert.Equal(TimeSpan.FromMinutes(20), pcState.Snapshot.TotalUsed);
        // The phone's offline allowance is released, so the PC sees no time set aside.
        Assert.Equal(TimeSpan.Zero, pcState.Snapshot.TotalReserved);
    }

    [Fact]
    public async Task UnpairedDevice_CannotBeUnpairedOrEditedAgain()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (_, pairing) = await factory.PairDeviceAsync(parent, child.Id, "Phone");

        (await parent.DeleteAsync($"/api/devices/{pairing.DeviceId}")).EnsureSuccessStatusCode();
        var unpairAgain = await parent.DeleteAsync($"/api/devices/{pairing.DeviceId}");
        var rename = await parent.PutAsJsonAsync($"/api/devices/{pairing.DeviceId}", new DeviceUpdateRequest("Phone 2", null), Json);

        Assert.Equal(HttpStatusCode.NotFound, unpairAgain.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
    }
}
