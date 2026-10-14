using System.Net;
using System.Net.Http.Json;
using ScreenTime.Server.Contracts;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

public class AccountTests
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task Name_CanBeSetAndCleared()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        (await parent.PutAsJsonAsync("/api/account/name", new UpdateNameRequest("  Ada  "), Json)).EnsureSuccessStatusCode();
        var named = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);
        (await parent.PutAsJsonAsync("/api/account/name", new UpdateNameRequest(""), Json)).EnsureSuccessStatusCode();
        var cleared = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);

        Assert.Equal("Ada", named!.Name);
        Assert.Null(cleared!.Name);
    }

    [Fact]
    public async Task Email_ChangesWithTheRightPassword_AndIsUsedToSignIn()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        var response = await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest("ada.new@example.com", Password), Json);
        var me = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);
        var newLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada.new@example.com", Password), Json);
        var oldLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", Password), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ada.new@example.com", me!.Email);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task Email_WithTheWrongPassword_IsRejected()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        var response = await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest("ada.new@example.com", "wrong-password!"), Json);
        var me = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CurrentPassword", await response.Content.ReadAsStringAsync());
        Assert.Equal("ada@example.com", me!.Email);
    }

    [Theory]
    [InlineData("bob@example.com")]
    [InlineData("not-an-email")]
    public async Task Email_TakenOrInvalid_IsRejected(string newEmail)
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync("bob@example.com");
        var parent = await factory.CreateParentAsync("ada@example.com");

        var response = await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest(newEmail, Password), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ada@example.com", (await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json))!.Email);
    }

    [Fact]
    public async Task Password_Changes_AndThisBrowserStaysSignedIn()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        var response = await parent.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest(Password, "a-brand-new-passphrase"), Json);
        var stillSignedIn = await parent.GetAsync("/api/children");
        var newLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", "a-brand-new-passphrase"), Json);
        var oldLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", Password), Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Theory]
    [InlineData("wrong-password!", "a-brand-new-passphrase", "CurrentPassword")]
    [InlineData(Password, "short", "PasswordTooShort")]
    public async Task Password_WithWrongCurrentOrTooShortNew_IsRejected(string current, string next, string errorKey)
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        var response = await parent.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest(current, next), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errorKey, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AccountEndpoints_NeedSignIn()
    {
        using var factory = new ServerFactory();

        var response = await factory.CreateClient().PutAsJsonAsync("/api/account/name", new UpdateNameRequest("Ada"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
