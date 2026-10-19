using System.Net;
using System.Net.Http.Json;
using Skjermvette.Server.Contracts;
using static Skjermvette.Server.Tests.ServerFactory;

namespace Skjermvette.Server.Tests;

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
    public async Task Email_ChangesOnceTheLinkSentToTheNewAddressIsOpened_AndIsUsedToSignIn()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        var response = await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest("ada.new@example.com", Password), Json);
        var before = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);
        var confirm = await factory.ConfirmEmailChangeAsync(parent, "ada.new@example.com");
        var openedAgain = await factory.ConfirmEmailChangeAsync(parent, "ada.new@example.com");
        var after = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);
        var newLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada.new@example.com", Password), Json);
        var oldLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", Password), Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("ada@example.com", before!.Email);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, openedAgain.StatusCode);
        Assert.Equal("ada.new@example.com", after!.Email);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task EmailChange_TellsTheCurrentAddress_WhoseLinkResetsThePasswordAndCancelsTheChange()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        (await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest("ada.new@example.com", Password), Json)).EnsureSuccessStatusCode();
        // The confirmation from registering, then the warning.
        var warning = (await factory.WaitForEmailsAsync("ada@example.com", 2))[1];
        var link = LinkIn(warning).Query;
        var reset = await factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(Guid.Parse(link["userId"]), link["token"], "a-brand-new-passphrase"), Json);
        var confirm = await factory.ConfirmEmailChangeAsync(factory.CreateClient(), "ada.new@example.com");

        Assert.Equal("Your Skjermvette email is about to change", warning.Subject);
        Assert.Contains("ada.new@example.com", warning.TextBody);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Contains("expired or was already used", await confirm.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmailChange_ToAnAddressTakenBeforeItsConfirmed_IsRejected()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");
        (await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest("shared@example.com", Password), Json)).EnsureSuccessStatusCode();
        var link = LinkIn(Assert.Single(await factory.WaitForEmailsAsync("shared@example.com"))).Query;
        await factory.CreateParentAsync("shared@example.com");

        var confirm = await factory.CreateClient().PostAsJsonAsync("/api/auth/confirm-email-change",
            new ConfirmEmailChangeRequest(Guid.Parse(link["userId"]), link["email"], link["token"]), Json);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Contains("DuplicateEmail", await confirm.Content.ReadAsStringAsync());
        Assert.Equal("ada@example.com", (await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json))!.Email);
    }

    [Fact]
    public async Task EmailChange_LinkForAnotherAddress_IsRejected()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");
        (await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest("ada.new@example.com", Password), Json)).EnsureSuccessStatusCode();
        var link = LinkIn(Assert.Single(await factory.WaitForEmailsAsync("ada.new@example.com"))).Query;

        var confirm = await parent.PostAsJsonAsync("/api/auth/confirm-email-change",
            new ConfirmEmailChangeRequest(Guid.Parse(link["userId"]), "mallory@example.com", link["token"]), Json);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Equal("ada@example.com", (await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json))!.Email);
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
    [InlineData("ADA@example.com")]
    public async Task Email_TakenOrInvalid_IsRejected(string newEmail)
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync("bob@example.com");
        var parent = await factory.CreateParentAsync("ada@example.com");

        var response = await parent.PutAsJsonAsync("/api/account/email", new ChangeEmailRequest(newEmail, Password), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ada@example.com", (await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json))!.Email);
        // Only the confirmations from registering.
        Assert.Equal(2, factory.Emails().Count);
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

    [Fact]
    public async Task PasswordChange_IsEmailed_WithALinkToResetItIfItWasntThem()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        (await parent.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest(Password, "a-brand-new-passphrase"), Json)).EnsureSuccessStatusCode();
        var notice = (await factory.WaitForEmailsAsync("ada@example.com", 2))[1];
        var link = LinkIn(notice).Query;
        var reset = await factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(Guid.Parse(link["userId"]), link["token"], "taking-it-back-now"), Json);

        Assert.Equal("Your Skjermvette password was changed", notice.Subject);
        Assert.Contains($"{PublicUrl}forgot-password", notice.TextBody);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
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
    public async Task WrongCurrentPasswords_CountTowardTheSignInLockout()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        for (var i = 0; i < 5; i++)
            await parent.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest("wrong-password!", "a-brand-new-passphrase"), Json);
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", Password), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Contains("Too many failed attempts", await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AccountEndpoints_NeedSignIn()
    {
        using var factory = new ServerFactory();

        var response = await factory.CreateClient().PutAsJsonAsync("/api/account/name", new UpdateNameRequest("Ada"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
