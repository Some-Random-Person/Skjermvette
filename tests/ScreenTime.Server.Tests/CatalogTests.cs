using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
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
        var indieGame = await (await parent.PostAsJsonAsync("/api/apps", new AppRequest("Indie game"), Json)).Content.ReadFromJsonAsync<AppResponse>(Json);
        var child = await CreateChildAsync(parent, DefaultChild() with { Rules = [new(indieGame!.Id, AppRuleKindEnum.Limited, 20)] });
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);
        await factory.UseAsync(pc, MatcherKindEnum.Process, "IndieGame-Win64.exe", 15);

        var unassigned = await parent.GetFromJsonAsync<List<UnassignedIdentifierResponse>>("/api/apps/unassigned", Json);
        var add = await parent.PostAsJsonAsync($"/api/apps/{indieGame.Id}/identifiers",
            new IdentifierDto(MatcherKindEnum.Process, "indiegame-win64.exe"), Json);
        var state = await StateAsync(pc);

        Assert.Single(unassigned!, u => u.RawIdentifier == "IndieGame-Win64.exe" && u.MinutesLast7Days == 15);
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(5), state.Snapshot.Remaining(indieGame.Id));
        Assert.Empty((await parent.GetFromJsonAsync<List<UnassignedIdentifierResponse>>("/api/apps/unassigned", Json))!);
    }

    [Fact]
    public async Task BuiltInCatalog_RecognisesFortnite()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent, DefaultChild() with { Rules = [new(SeedCatalog.Fortnite, AppRuleKindEnum.Limited, 45)] });
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);

        var sync = await factory.UseAsync(pc, MatcherKindEnum.Process, @"C:\Program Files\Epic Games\Fortnite\FortniteClient-Win64-Shipping.exe", 15);

        Assert.Equal(TimeSpan.FromMinutes(30), sync.Snapshot.Remaining(SeedCatalog.Fortnite));
    }

    [Fact]
    public async Task FamilyMapping_WinsOverABuiltInIdentifierAddedLater()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var me = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);
        var own = await (await parent.PostAsJsonAsync("/api/apps", new AppRequest("Our Fortnite"), Json)).Content.ReadFromJsonAsync<AppResponse>(Json);
        // Simulates a mapping the family made before Fortnite was in the built-in catalog
        // (the API refuses it now, because the built-in catalog already has it).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AppIdentifiers.Add(new AppIdentifier
            {
                AppDefinitionId = own!.Id,
                FamilyId = me!.FamilyId,
                Kind = MatcherKindEnum.Process,
                Value = "fortniteclient-win64-shipping.exe",
            });
            await db.SaveChangesAsync();
        }
        var child = await CreateChildAsync(parent, DefaultChild() with { Rules = [new(own!.Id, AppRuleKindEnum.Limited, 20)] });
        var (pc, _) = await factory.PairDeviceAsync(parent, child.Id);

        var sync = await factory.UseAsync(pc, MatcherKindEnum.Process, "FortniteClient-Win64-Shipping.exe", 15);
        var state = await StateAsync(pc);

        Assert.Equal(TimeSpan.FromMinutes(5), sync.Snapshot.Remaining(own.Id));
        Assert.DoesNotContain(state.Catalog.Single(a => a.Id == SeedCatalog.Fortnite).Matchers,
            m => m.Value == "fortniteclient-win64-shipping.exe");
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
