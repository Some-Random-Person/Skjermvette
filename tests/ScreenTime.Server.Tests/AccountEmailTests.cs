using System.Net;
using System.Net.Http.Json;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

/// <summary>Confirming a new account's email, and resetting a forgotten password, through links sent by email.</summary>
public class AccountEmailTests
{
    private const string Email = "ada@example.com";
    private const string Password = "correct-horse-battery";
    private const string NewPassword = "a-brand-new-passphrase";

    private static Task<HttpResponseMessage> LoginAsync(ServerFactory factory, string password = Password) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(Email, password), Json);

    private static async Task RegisterAsync(ServerFactory factory) =>
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(Email, Password), Json))
            .EnsureSuccessStatusCode();

    private static async Task<Dictionary<string, string>> ForgotPasswordAsync(ServerFactory factory, int emailNumber = 1)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new EmailRequest(Email), Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var emails = await factory.WaitForEmailsAsync(Email, emailNumber);
        return LinkIn(emails[emailNumber - 1]).Query;
    }

    private static Task<HttpResponseMessage> ResetAsync(ServerFactory factory, Dictionary<string, string> link, string newPassword = NewPassword) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(Guid.Parse(link["userId"]), link["token"], newPassword), Json);

    [Fact]
    public async Task Register_SendsAConfirmationLink_AndSignInWaitsForIt()
    {
        using var factory = new ServerFactory();
        await RegisterAsync(factory);

        var beforeConfirming = await LoginAsync(factory);
        var email = Assert.Single(await factory.WaitForEmailsAsync(Email));
        var (url, query) = LinkIn(email);
        var confirm = await factory.CreateClient().PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest(Guid.Parse(query["userId"]), query["token"]), Json);
        var afterConfirming = await LoginAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, beforeConfirming.StatusCode);
        Assert.Contains("Confirm your email", await beforeConfirming.Content.ReadAsStringAsync());
        Assert.Equal("Confirm your email for Screen Time", email.Subject);
        Assert.Equal($"{PublicUrl}confirm-email", url.GetLeftPart(UriPartial.Path));
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterConfirming.StatusCode);
    }

    [Fact]
    public async Task Unconfirmed_WithTheWrongPassword_IsStill401()
    {
        using var factory = new ServerFactory();
        await RegisterAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(factory, "wrong-password!")).StatusCode);
    }

    [Theory]
    [InlineData("not-base64!")]
    [InlineData("dGFtcGVyZWQ")]
    public async Task ConfirmEmail_WithABadToken_IsRejected(string token)
    {
        using var factory = new ServerFactory();
        await RegisterAsync(factory);
        var userId = Guid.Parse(LinkIn(Assert.Single(await factory.WaitForEmailsAsync(Email))).Query["userId"]);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(userId, token), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("expired or was already used", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await LoginAsync(factory)).StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_OnlySendsToUnconfirmedAccounts_AndRepliesTheSameEitherWay()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync("bob@example.com");
        await RegisterAsync(factory);
        var client = factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new EmailRequest("nobody@example.com"), Json);
        var confirmed = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new EmailRequest("bob@example.com"), Json);
        var unconfirmed = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new EmailRequest(Email), Json);
        // Emails are sent in order, so once Ada's second one is there, nothing else is still on its way.
        var toAda = await factory.WaitForEmailsAsync(Email, 2);

        Assert.All([unknown, confirmed, unconfirmed], r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Equal(2, toAda.Count);
        Assert.Equal(["bob@example.com", Email, Email], factory.Emails().Select(m => m.To.Mailboxes.Single().Address));
    }

    [Fact]
    public async Task ForgotPassword_RepliesTheSameForAnUnknownAddress_AndSendsNothing()
    {
        using var factory = new ServerFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new EmailRequest("nobody@example.com"), Json);
        await factory.CreateParentAsync(Email);
        await factory.WaitForEmailsAsync(Email);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([Email], factory.Emails().Select(m => m.To.Mailboxes.Single().Address));
    }

    [Fact]
    public async Task ResetPassword_SetsTheNewPassword_AndTheLinkWorksOnce()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        await factory.WaitForEmailsAsync(Email);

        var link = await ForgotPasswordAsync(factory, emailNumber: 2);
        var reset = await ResetAsync(factory, link);
        var again = await ResetAsync(factory, link, "yet-another-passphrase");
        var notice = (await factory.WaitForEmailsAsync(Email, 3))[2];

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal("Your Screen Time password was changed", notice.Subject);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("expired or was already used", await again.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(factory, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(factory)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithATooShortPassword_IsRejected_AndTheLinkStillWorks()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        await factory.WaitForEmailsAsync(Email);
        var link = await ForgotPasswordAsync(factory, emailNumber: 2);

        var tooShort = await ResetAsync(factory, link, "short");
        var reset = await ResetAsync(factory, link);

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Contains("PasswordTooShort", await tooShort.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_SignsOutOtherBrowsers_AndLiftsALockout()
    {
        using var factory = new ServerFactory();
        var otherBrowser = await factory.CreateParentAsync(Email);
        await factory.WaitForEmailsAsync(Email);
        for (var i = 0; i < 5; i++)
            await LoginAsync(factory, "wrong-password!");
        var lockedOut = await LoginAsync(factory);

        var reset = await ResetAsync(factory, await ForgotPasswordAsync(factory, emailNumber: 2));
        factory.Clock.Advance(ParentSession.RecheckInterval + TimeSpan.FromSeconds(1));

        Assert.Contains("Too many failed attempts", await lockedOut.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherBrowser.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(factory, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ConfirmsTheEmail_SinceTheLinkWentToIt()
    {
        using var factory = new ServerFactory();
        await RegisterAsync(factory);
        await factory.WaitForEmailsAsync(Email);

        await ResetAsync(factory, await ForgotPasswordAsync(factory, emailNumber: 2));

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(factory, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ForAnUnknownUser_IsRejected()
    {
        using var factory = new ServerFactory();

        var response = await ResetAsync(factory, new() { ["userId"] = Guid.NewGuid().ToString(), ["token"] = "dGFtcGVyZWQ" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("expired or was already used", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ForgotPassword_IsRateLimited()
    {
        using var factory = new ServerFactory();
        var client = factory.CreateClient();

        var responses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
            responses.Add((await client.PostAsJsonAsync("/api/auth/forgot-password", new EmailRequest("nobody@example.com"), Json)).StatusCode);

        Assert.All(responses[..10], s => Assert.Equal(HttpStatusCode.NoContent, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[10]);
    }
}
