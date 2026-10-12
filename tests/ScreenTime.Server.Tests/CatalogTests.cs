using System.Net;
using System.Net.Http.Json;
using ScreenTime.Core.Models;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

public class CatalogTests
{
    [Fact]
    public async Task UnknownIdentifier_ShowsAsUnassigned_AndCountsOnceMapped()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var fortnite = await (await parent.PostAsJsonAsync("/api/apps", new AppRequest("Fortnite"), Json)).Content.ReadFromJsonAsync<AppResponse>(Json);
        var child = await CreateChildAsync(parent, DefaultChild() with { Rules = [new(fortnite!.Id, AppRuleKindEnum.Limited, 20)] });
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);
        await factory.UseAsync(pc, MatcherKindEnum.Process, "FortniteClient-Win64-Shipping.exe", 15);

        var unassigned = await parent.GetFromJsonAsync<List<UnassignedIdentifierResponse>>("/api/apps/unassigned", Json);
        var add = await parent.PostAsJsonAsync($"/api/apps/{fortnite.Id}/identifiers",
            new IdentifierDto(MatcherKindEnum.Process, "fortniteclient-win64-shipping.exe"), Json);
        var state = await StateAsync(pc);

        Assert.Single(unassigned!, u => u.RawIdentifier == "FortniteClient-Win64-Shipping.exe" && u.MinutesLast7Days == 15);
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(5), state.Snapshot.Remaining(fortnite.Id));
        Assert.Empty((await parent.GetFromJsonAsync<List<UnassignedIdentifierResponse>>("/api/apps/unassigned", Json))!);
    }

    [Fact]
    public async Task IdentifierAlreadyOnAnotherApp_IsConflict()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var custom = await (await parent.PostAsJsonAsync("/api/apps", new AppRequest("Videos"), Json)).Content.ReadFromJsonAsync<AppResponse>(Json);

        var response = await parent.PostAsJsonAsync($"/api/apps/{custom!.Id}/identifiers", new IdentifierDto(MatcherKindEnum.Domain, "YouTube.com"), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task FamilyIdentifiers_AreNotVisibleToOtherFamilies()
    {
        using var factory = new ServerFactory();
        var ada = await factory.CreateParentAsync("ada@example.com");
        var bob = await factory.CreateParentAsync("bob@example.com");

        (await ada.PostAsJsonAsync($"/api/apps/{SeedCatalog.YouTube}/identifiers", new IdentifierDto(MatcherKindEnum.Process, "freetube.exe"), Json)).EnsureSuccessStatusCode();
        var bobsYouTube = (await bob.GetFromJsonAsync<List<AppResponse>>("/api/apps", Json))!.Single(a => a.Id == SeedCatalog.YouTube);
        var adasYouTube = (await ada.GetFromJsonAsync<List<AppResponse>>("/api/apps", Json))!.Single(a => a.Id == SeedCatalog.YouTube);

        Assert.DoesNotContain(bobsYouTube.Identifiers, i => i.Value == "freetube.exe");
        Assert.Contains(adasYouTube.Identifiers, i => i.Value == "freetube.exe" && !i.IsBuiltIn);
    }

    [Fact]
    public async Task BuiltInApps_CannotBeDeleted()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await parent.DeleteAsync($"/api/apps/{SeedCatalog.YouTube}")).StatusCode);
    }
}
