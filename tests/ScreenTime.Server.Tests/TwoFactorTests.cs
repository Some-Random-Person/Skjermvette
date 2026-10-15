using System.Net;
using System.Net.Http.Json;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

/// <summary>The second sign-in step: an authenticator app with recovery codes, or a code sent by email.</summary>
public class TwoFactorTests
{
    private const string Email = "ada@example.com";
    private static readonly TimeSpan PastRecheck = ParentSession.RecheckInterval + TimeSpan.FromSeconds(1);

    private static async Task<LoginResponse> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Email, DefaultPassword), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
    }

    private static Task<HttpResponseMessage> CodeAsync(HttpClient client, TwoFactorMethodEnum method, string code, bool remember = false) =>
        client.PostAsJsonAsync("/api/auth/login/code", new LoginCodeRequest(method, code, remember), Json);

    private static async Task<HttpStatusCode> ChildrenStatusAsync(HttpClient client) => (await client.GetAsync("/api/children")).StatusCode;

    [Fact]
    public async Task WithoutApp_TheEmailedCode_SignsIn()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        var client = factory.CreateClient();

        var login = await LoginAsync(client);
        var beforeCode = await ChildrenStatusAsync(client);
        var code = await factory.WaitForCodeAsync(Email, 2);
        var response = await CodeAsync(client, TwoFactorMethodEnum.Email, code);

        Assert.Equal(new LoginResponse(null, TwoFactorMethodEnum.Email), login);
        Assert.Equal(HttpStatusCode.Unauthorized, beforeCode);
        Assert.Equal(Email, (await response.Content.ReadFromJsonAsync<MeResponse>(Json))!.Email);
        Assert.Equal(HttpStatusCode.OK, await ChildrenStatusAsync(client));
    }

    [Fact]
    public async Task EmailedCode_WorksOnce()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        var first = factory.CreateClient();
        await LoginAsync(first);
        var code = await factory.NewEmailCodeAsync(Email);
        (await CodeAsync(first, TwoFactorMethodEnum.Email, code)).EnsureSuccessStatusCode();
        var second = factory.CreateClient();
        await LoginAsync(second);

        var reused = await CodeAsync(second, TwoFactorMethodEnum.Email, code);

        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task EmailedCode_ExpiresAfterTenMinutes()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        var client = factory.CreateClient();
        await LoginAsync(client);
        var code = await factory.NewEmailCodeAsync(Email);

        factory.Clock.Advance(ParentSession.EmailCodeLifespan + TimeSpan.FromSeconds(1));
        var response = await CodeAsync(client, TwoFactorMethodEnum.Email, code);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Code_WithoutThePasswordFirst_Is401()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        var code = await factory.NewEmailCodeAsync(Email);

        var response = await CodeAsync(factory.CreateClient(), TwoFactorMethodEnum.Email, code);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongCodes_CountTowardTheLockout()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        var client = factory.CreateClient();
        await LoginAsync(client);

        for (var i = 0; i < 5; i++)
            await CodeAsync(client, TwoFactorMethodEnum.Email, "000000");
        var rightCode = await CodeAsync(client, TwoFactorMethodEnum.Email, await factory.NewEmailCodeAsync(Email));

        Assert.Equal(HttpStatusCode.Unauthorized, rightCode.StatusCode);
        Assert.Contains("Too many failed attempts", await rightCode.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SendingTheCodeAgain_EmailsANewOne()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        var client = factory.CreateClient();
        await LoginAsync(client);
        var firstCode = await factory.WaitForCodeAsync(Email, 2);

        var resend = await client.PostAsync("/api/auth/login/email-code", null);
        var newCode = await factory.WaitForCodeAsync(Email, 3);

        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CodeAsync(client, TwoFactorMethodEnum.Email, newCode)).StatusCode);
        Assert.NotEqual(firstCode, newCode);
    }

    [Fact]
    public async Task RememberedBrowser_SkipsTheSecondStep_For30Days()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        var client = factory.CreateClient();
        await LoginAsync(client);
        (await CodeAsync(client, TwoFactorMethodEnum.Email, await factory.NewEmailCodeAsync(Email), remember: true)).EnsureSuccessStatusCode();
        await client.PostAsync("/api/auth/logout", null);

        var remembered = await LoginAsync(client);
        var otherBrowser = await LoginAsync(factory.CreateClient());
        factory.Clock.Advance(ParentSession.RememberBrowserLifespan + TimeSpan.FromMinutes(1));
        var after30Days = await LoginAsync(client);

        Assert.Equal(Email, remembered.Me?.Email);
        Assert.Null(remembered.SecondStep);
        Assert.Equal(TwoFactorMethodEnum.Email, otherBrowser.SecondStep);
        Assert.Equal(TwoFactorMethodEnum.Email, after30Days.SecondStep);
    }

    [Fact]
    public async Task ForgettingBrowsers_AsksForTheSecondStepAgain_AndSignsOtherBrowsersOut()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var rememberedBrowser = factory.CreateClient();
        await LoginAsync(rememberedBrowser);
        (await CodeAsync(rememberedBrowser, TwoFactorMethodEnum.Email, await factory.NewEmailCodeAsync(Email), remember: true)).EnsureSuccessStatusCode();

        var forget = await parent.PostAsync("/api/account/two-factor/forget-browsers", null);
        factory.Clock.Advance(PastRecheck);

        Assert.Equal(HttpStatusCode.NoContent, forget.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await ChildrenStatusAsync(parent));
        Assert.Equal(HttpStatusCode.Unauthorized, await ChildrenStatusAsync(rememberedBrowser));
        Assert.Equal(TwoFactorMethodEnum.Email, (await LoginAsync(rememberedBrowser)).SecondStep);
    }

    [Fact]
    public async Task App_IsSetUpWithACode_ThenAskedForAtSignIn_AndEmailStillWorks()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var otherBrowser = factory.CreateClient();
        await factory.SignInAsync(otherBrowser, Email);

        var setup = await (await parent.PostAsync("/api/account/two-factor/app/setup", null)).Content.ReadFromJsonAsync<AuthenticatorSetupResponse>(Json);
        var wrongCode = await parent.PostAsJsonAsync("/api/account/two-factor/app", new EnableAuthenticatorRequest("000000"), Json);
        var enable = await parent.PostAsJsonAsync("/api/account/two-factor/app", new EnableAuthenticatorRequest(Totp.Code(setup!.Key)), Json);
        var recoveryCodes = (await enable.Content.ReadFromJsonAsync<RecoveryCodesResponse>(Json))!.Codes;
        var status = await parent.GetFromJsonAsync<TwoFactorResponse>("/api/account/two-factor", Json);
        factory.Clock.Advance(PastRecheck);

        Assert.StartsWith("otpauth://totp/Screen%20Time%3Aada%40example.com?secret=", setup.Uri);
        Assert.StartsWith("<svg", setup.QrCodeSvg.TrimStart());
        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        Assert.Equal(ParentSession.RecoveryCodeCount, recoveryCodes.Count);
        Assert.Equal(new TwoFactorResponse(true, ParentSession.RecoveryCodeCount), status);
        Assert.Equal(HttpStatusCode.OK, await ChildrenStatusAsync(parent));
        Assert.Equal(HttpStatusCode.Unauthorized, await ChildrenStatusAsync(otherBrowser));

        var appBrowser = factory.CreateClient();
        Assert.Equal(new LoginResponse(null, TwoFactorMethodEnum.App), await LoginAsync(appBrowser));
        Assert.Equal(HttpStatusCode.OK, (await CodeAsync(appBrowser, TwoFactorMethodEnum.App, Totp.Code(setup.Key))).StatusCode);

        var emailBrowser = factory.CreateClient();
        await LoginAsync(emailBrowser);
        Assert.Equal(HttpStatusCode.NoContent, (await emailBrowser.PostAsync("/api/auth/login/email-code", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CodeAsync(emailBrowser, TwoFactorMethodEnum.Email, await factory.WaitForCodeAsync(Email, 3))).StatusCode);
    }

    [Fact]
    public async Task AppCode_BeforeTheAppIsConfirmed_DoesNotSignIn()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var setup = await (await parent.PostAsync("/api/account/two-factor/app/setup", null)).Content.ReadFromJsonAsync<AuthenticatorSetupResponse>(Json);
        var client = factory.CreateClient();

        var login = await LoginAsync(client);
        var response = await CodeAsync(client, TwoFactorMethodEnum.App, Totp.Code(setup!.Key));

        Assert.Equal(TwoFactorMethodEnum.Email, login.SecondStep);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Setup_WhenTheAppIsOn_IsValidationProblem()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        await EnableAppAsync(parent);

        var response = await parent.PostAsync("/api/account/two-factor/app/setup", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecoveryCode_SignsInOnce_AndSignsOtherBrowsersOut()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var (_, recoveryCodes) = await EnableAppAsync(parent);
        var client = factory.CreateClient();
        await LoginAsync(client);

        var used = await CodeAsync(client, TwoFactorMethodEnum.RecoveryCode, recoveryCodes[0]);
        factory.Clock.Advance(PastRecheck);
        var again = factory.CreateClient();
        await LoginAsync(again);
        var reused = await CodeAsync(again, TwoFactorMethodEnum.RecoveryCode, recoveryCodes[0]);
        var status = await client.GetFromJsonAsync<TwoFactorResponse>("/api/account/two-factor", Json);

        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(ParentSession.RecoveryCodeCount - 1, status!.RecoveryCodesLeft);
        Assert.Equal(HttpStatusCode.Unauthorized, await ChildrenStatusAsync(parent));
    }

    [Fact]
    public async Task RecoveryCodes_Regenerated_ReplaceTheOldOnes()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var (_, oldCodes) = await EnableAppAsync(parent);

        var response = await parent.PostAsync("/api/account/two-factor/recovery-codes", null);
        var newCodes = (await response.Content.ReadFromJsonAsync<RecoveryCodesResponse>(Json))!.Codes;
        var client = factory.CreateClient();
        await LoginAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, (await CodeAsync(client, TwoFactorMethodEnum.RecoveryCode, oldCodes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CodeAsync(client, TwoFactorMethodEnum.RecoveryCode, newCodes[0])).StatusCode);
    }

    [Fact]
    public async Task RemovingTheApp_NeedsPasswordAndCode_ThenCodesAreEmailedAgain()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var (key, _) = await EnableAppAsync(parent);

        var wrongPassword = await parent.SendAsync(Remove("wrong-password!", Totp.Code(key)));
        var wrongCode = await parent.SendAsync(Remove(DefaultPassword, "000000"));
        var removed = await parent.SendAsync(Remove(DefaultPassword, Totp.Code(key)));
        var status = await parent.GetFromJsonAsync<TwoFactorResponse>("/api/account/two-factor", Json);
        var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var oldAppCode = await CodeAsync(client, TwoFactorMethodEnum.App, Totp.Code(key));

        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Contains("CurrentPassword", await wrongPassword.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        Assert.Contains("Code", await wrongCode.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(new TwoFactorResponse(false, 0), status);
        Assert.Equal(TwoFactorMethodEnum.Email, login.SecondStep);
        Assert.Equal(HttpStatusCode.Unauthorized, oldAppCode.StatusCode);

        static HttpRequestMessage Remove(string password, string code) => new(HttpMethod.Delete, "/api/account/two-factor/app")
        {
            Content = JsonContent.Create(new DisableAuthenticatorRequest(password, code), options: Json),
        };
    }

    [Fact]
    public async Task RemovingTheApp_WithARecoveryCode_Works()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);
        var (_, recoveryCodes) = await EnableAppAsync(parent);

        var response = await parent.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/two-factor/app")
        {
            Content = JsonContent.Create(new DisableAuthenticatorRequest(DefaultPassword, recoveryCodes[0]), options: Json),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AppAndRecoveryCodeChanges_AreEmailed_WithALinkToResetThePassword_ButForgettingBrowsersIsNot()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync(Email);

        // First, so an email from it would come before the others (the outbox sends in order).
        (await parent.PostAsync("/api/account/two-factor/forget-browsers", null)).EnsureSuccessStatusCode();
        var (key, _) = await EnableAppAsync(parent);
        (await parent.PostAsync("/api/account/two-factor/recovery-codes", null)).EnsureSuccessStatusCode();
        (await parent.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/two-factor/app")
        {
            Content = JsonContent.Create(new DisableAuthenticatorRequest(DefaultPassword, Totp.Code(key)), options: Json),
        })).EnsureSuccessStatusCode();
        // The first email is the address confirmation from registering.
        var notices = (await factory.WaitForEmailsAsync(Email, 4)).Skip(1).ToList();
        var link = LinkIn(notices[^1]).Query;
        var reset = await factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(Guid.Parse(link["userId"]), link["token"], "taking-it-back-now"), Json);

        Assert.All(notices, n => Assert.Equal("Your Screen Time sign-in security changed", n.Subject));
        Assert.Collection(notices,
            n => Assert.Contains("authenticator app was just turned on", n.TextBody),
            n => Assert.Contains("New recovery codes were just made", n.TextBody),
            n => Assert.Contains("authenticator app was just turned off", n.TextBody));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }
}
