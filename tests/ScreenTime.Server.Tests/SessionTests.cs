using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data.Entities;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

/// <summary>Parent sign-ins are re-checked against the database every <see cref="ParentSession.RecheckInterval"/>.</summary>
public class SessionTests
{
    private const string Email = "ada@example.com";
    private const string Password = "correct-horse-battery";
    private static readonly TimeSpan PastRecheck = ParentSession.RecheckInterval + TimeSpan.FromSeconds(1);

    private static async Task<HttpClient> SignInElsewhereAsync(ServerFactory factory)
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Email, Password), Json)).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task UnchangedSignIn_KeepsWorkingAfterTheRecheck_WithItsFamily()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var child = await CreateChildAsync(parent);

        factory.Clock.Advance(PastRecheck);
        var children = await parent.GetFromJsonAsync<List<ChildResponse>>("/api/children", Json);
        factory.Clock.Advance(PastRecheck);
        var again = await parent.GetAsync($"/api/children/{child.Id}");

        Assert.Equal([child.Id], children!.Select(c => c.Id));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task PasswordChange_SignsOutOtherBrowsers_ButNotThisOne()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var otherBrowser = await SignInElsewhereAsync(factory);

        (await parent.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest(Password, "a-brand-new-passphrase"), Json))
            .EnsureSuccessStatusCode();
        var beforeRecheck = await otherBrowser.GetAsync("/api/children");
        factory.Clock.Advance(PastRecheck);

        Assert.Equal(HttpStatusCode.OK, beforeRecheck.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherBrowser.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync("/api/children")).StatusCode);
    }

    [Fact]
    public async Task EmailChange_SignsOutOtherBrowsers()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var otherBrowser = await SignInElsewhereAsync(factory);

        (await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest("ada.new@example.com", Password), Json))
            .EnsureSuccessStatusCode();
        (await factory.ConfirmEmailChangeAsync(parent, "ada.new@example.com")).EnsureSuccessStatusCode();
        factory.Clock.Advance(PastRecheck);

        Assert.Equal(HttpStatusCode.Unauthorized, (await otherBrowser.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync("/api/children")).StatusCode);
    }

    [Fact]
    public async Task DeletedAccount_IsSignedOut()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ParentUser>>();
            Assert.True((await users.DeleteAsync((await users.FindByEmailAsync(Email))!)).Succeeded);
        }

        factory.Clock.Advance(PastRecheck);

        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/children")).StatusCode);
    }
}
