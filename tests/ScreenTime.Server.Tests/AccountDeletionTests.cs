using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScreenTime.Core.Models;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

public class AccountDeletionTests
{
    private const string Password = "correct-horse-battery";

    private static Task<HttpResponseMessage> DeleteAccountAsync(HttpClient parent, string password, string code) =>
        parent.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(password, code), options: Json),
        });

    /// <summary>Row counts of every table a family's data lives in, across all families.</summary>
    private static async Task<Dictionary<string, int>> CountRowsAsync(ServerFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new()
        {
            ["Families"] = await db.Families.CountAsync(),
            ["Parents"] = await db.Users.CountAsync(),
            ["Children"] = await db.Children.CountAsync(),
            ["AppRules"] = await db.AppRules.CountAsync(),
            ["AppDefinitions"] = await db.AppDefinitions.CountAsync(),
            ["AppIdentifiers"] = await db.AppIdentifiers.CountAsync(),
            ["Devices"] = await db.Devices.CountAsync(),
            ["DeviceLeases"] = await db.DeviceLeases.CountAsync(),
            ["DeviceLeaseApps"] = await db.Set<DeviceLeaseApp>().CountAsync(),
            ["PairingCodes"] = await db.PairingCodes.CountAsync(),
            ["UsageRecords"] = await db.UsageRecords.CountAsync(),
            ["ExtraTimeGrants"] = await db.ExtraTimeGrants.CountAsync(),
        };
    }

    /// <summary>A family with a child and a paired device with some usage.</summary>
    private static async Task<(HttpClient Parent, HttpClient Device)> CreateFamilyAsync(ServerFactory factory, string email)
    {
        var parent = await factory.CreateParentAsync(email);
        var child = await CreateChildAsync(parent);
        var (device, _) = await factory.PairDeviceAsync(parent, child.Id);
        await factory.UseAsync(device, MatcherKindEnum.Domain, "www.youtube.com", 10);
        return (parent, device);
    }

    [Fact]
    public async Task WrongPassword_IsRejected_AndNothingIsDeleted()
    {
        using var factory = new ServerFactory();
        var (parent, device) = await CreateFamilyAsync(factory, "ada@example.com");
        var before = await CountRowsAsync(factory);

        var response = await DeleteAccountAsync(parent, "wrong-password!", await factory.NewEmailCodeAsync("ada@example.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CurrentPassword", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await CountRowsAsync(factory));
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/device/state")).StatusCode);
    }

    [Fact]
    public async Task WrongCode_IsRejected_NothingIsDeleted_AndItCountsTowardTheLockout()
    {
        using var factory = new ServerFactory();
        var (parent, _) = await CreateFamilyAsync(factory, "ada@example.com");
        var before = await CountRowsAsync(factory);

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 5; i++)
            responses.Add(await DeleteAccountAsync(parent, Password, "000000"));
        var rightCode = await DeleteAccountAsync(parent, Password, await factory.NewEmailCodeAsync("ada@example.com"));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Contains("Code", await responses[0].Content.ReadAsStringAsync());
        Assert.Contains("Too many wrong attempts", await responses[4].Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, rightCode.StatusCode);
        Assert.Equal(before, await CountRowsAsync(factory));
    }

    [Fact]
    public async Task EmailedCode_AskedForFromSettings_DeletesTheAccount()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        var sent = await parent.PostAsync("/api/account/deletion/email-code", null);
        // The first code was emailed when signing in.
        var code = await factory.WaitForCodeAsync("ada@example.com", 2);
        var response = await DeleteAccountAsync(parent, Password, code);

        Assert.Equal(HttpStatusCode.NoContent, sent.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/children")).StatusCode);
    }

    [Fact]
    public async Task WithTheApp_AnAppOrRecoveryCodeWorks()
    {
        using var factory = new ServerFactory();
        var ada = await factory.CreateParentAsync("ada@example.com");
        var bob = await factory.CreateParentAsync("bob@example.com");
        var (adaKey, _) = await EnableAppAsync(ada);
        var (_, bobCodes) = await EnableAppAsync(bob);

        var withAppCode = await DeleteAccountAsync(ada, Password, Totp.Code(adaKey));
        var withRecoveryCode = await DeleteAccountAsync(bob, Password, bobCodes[0]);

        Assert.Equal(HttpStatusCode.NoContent, withAppCode.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withRecoveryCode.StatusCode);
    }

    [Fact]
    public async Task LastParent_DeletesTheFamilyAndEverythingUnderIt_AndLeavesOtherFamiliesAlone()
    {
        using var factory = new ServerFactory();
        var (otherParent, otherDevice) = await CreateFamilyAsync(factory, "bob@example.com");
        var onlyOtherFamily = await CountRowsAsync(factory);

        var parent = await factory.CreateParentAsync("ada@example.com");
        var ownApp = await (await parent.PostAsJsonAsync("/api/apps", new AppRequest("Homework Hub"), Json))
            .Content.ReadFromJsonAsync<AppResponse>(Json);
        (await parent.PostAsJsonAsync($"/api/apps/{ownApp!.Id}/identifiers", new IdentifierDto(MatcherKindEnum.Domain, "homework.example"), Json))
            .EnsureSuccessStatusCode();
        (await parent.PostAsJsonAsync($"/api/apps/{SeedCatalog.YouTube}/identifiers", new IdentifierDto(MatcherKindEnum.Domain, "yt.example"), Json))
            .EnsureSuccessStatusCode();
        var child = await CreateChildAsync(parent, DefaultChild() with
        {
            Rules = [.. DefaultChild().Rules, new AppRuleDto(ownApp.Id, AppRuleKindEnum.Limited, 20)],
        });
        var (device, _) = await factory.PairDeviceAsync(parent, child.Id);
        await factory.UseAsync(device, MatcherKindEnum.Domain, "homework.example", 10);
        (await parent.PostAsync($"/api/children/{child.Id}/pairing-codes", null)).EnsureSuccessStatusCode();
        (await parent.PostAsJsonAsync($"/api/children/{child.Id}/extra-time", new ExtraTimeRequest(15), Json)).EnsureSuccessStatusCode();
        var withBothFamilies = await CountRowsAsync(factory);

        var info = await parent.GetFromJsonAsync<AccountDeletionResponse>("/api/account/deletion", Json);
        var response = await DeleteAccountAsync(parent, Password, await factory.NewEmailCodeAsync("ada@example.com"));

        // Every table held some of this family's rows, so each cascade path below was exercised.
        Assert.All(withBothFamilies, table => Assert.True(table.Value > onlyOtherFamily[table.Key], $"No {table.Key} rows were created."));
        Assert.Equal(new AccountDeletionResponse(true, 1, 1), info);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(onlyOtherFamily, await CountRowsAsync(factory));
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/device/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otherParent.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.GetAsync("/api/device/state")).StatusCode);
    }

    [Fact]
    public async Task WithAnotherParent_OnlyThisAccountIsDeleted()
    {
        using var factory = new ServerFactory();
        var (parent, device) = await CreateFamilyAsync(factory, "ada@example.com");
        var familyId = (await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json))!.FamilyId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ParentUser>>();
            var second = new ParentUser { UserName = "bob@example.com", Email = "bob@example.com", EmailConfirmed = true, FamilyId = familyId };
            Assert.True((await users.CreateAsync(second, Password)).Succeeded);
        }

        var info = await parent.GetFromJsonAsync<AccountDeletionResponse>("/api/account/deletion", Json);
        var response = await DeleteAccountAsync(parent, Password, await factory.NewEmailCodeAsync("ada@example.com"));
        var adaLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", Password), Json);
        var bob = factory.CreateClient();
        await factory.SignInAsync(bob, "bob@example.com", Password);

        Assert.Equal(new AccountDeletionResponse(false, 1, 1), info);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, adaLogin.StatusCode);
        Assert.Single((await bob.GetFromJsonAsync<List<ChildResponse>>("/api/children", Json))!);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/device/state")).StatusCode);
    }

    [Fact]
    public async Task OtherBrowsers_AreSignedOut()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");
        var otherBrowser = factory.CreateClient();
        await factory.SignInAsync(otherBrowser, "ada@example.com", Password);

        (await DeleteAccountAsync(parent, Password, await factory.NewEmailCodeAsync("ada@example.com"))).EnsureSuccessStatusCode();
        factory.Clock.Advance(ParentSession.RecheckInterval + TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await otherBrowser.GetAsync("/api/children")).StatusCode);
    }
}
