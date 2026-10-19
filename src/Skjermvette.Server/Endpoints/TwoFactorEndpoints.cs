using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using Skjermvette.Server.Auth;
using Skjermvette.Server.Contracts;
using Skjermvette.Server.Data.Entities;
using Skjermvette.Server.Services;
using static Skjermvette.Server.Endpoints.AccountEndpoints;

namespace Skjermvette.Server.Endpoints;

/// <summary>
/// The second sign-in step, on Settings → Security: an authenticator app with recovery codes, or (without an app)
/// a code sent by email. Turning the app on or off changes the security stamp, which signs other browsers out.
/// Turning the app on or off and new recovery codes are emailed to the parent, with a link to reset the password if it wasn't them.
/// </summary>
public static class TwoFactorEndpoints
{
    private const string Issuer = "Skjermvette";

    public static void MapTwoFactorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account/two-factor").WithTags("Account").RequireAuthorization(AppClaims.ParentPolicy);
        group.MapGet("/", GetAsync);
        group.MapPost("/app/setup", SetupAppAsync);
        group.MapPost("/app", EnableAppAsync);
        group.MapDelete("/app", DisableAppAsync);
        group.MapPost("/recovery-codes", RegenerateRecoveryCodesAsync);
        group.MapPost("/forget-browsers", ForgetBrowsersAsync);
    }

    /// <summary>Which second step the parent has (the authenticator app or an emailed code), and how many recovery codes are left.</summary>
    private static async Task<Results<Ok<TwoFactorResponse>, UnauthorizedHttpResult>> GetAsync(
        ClaimsPrincipal principal, UserManager<ParentUser> users) =>
        await users.GetUserAsync(principal) is { } user
            ? TypedResults.Ok(new TwoFactorResponse(user.TwoFactorEnabled, await users.CountRecoveryCodesAsync(user)))
            : TypedResults.Unauthorized();

    /// <summary>
    /// The key to add to an authenticator app, as text, an <c>otpauth://</c> link and a QR code. Nothing changes
    /// until a code from the app is confirmed (<c>POST /api/account/two-factor/app</c>).
    /// </summary>
    private static async Task<Results<Ok<AuthenticatorSetupResponse>, ValidationProblem, UnauthorizedHttpResult>> SetupAppAsync(
        ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (user.TwoFactorEnabled)
            return TypedResults.ValidationProblem(Error("App", "An authenticator app is already set up. Remove it first."));

        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            // Only the first time: removing the app makes a new key straight away. Making a key changes the
            // security stamp, so this browser's sign-in is renewed.
            await users.ResetAuthenticatorKeyAsync(user);
            await ParentSession.SignInAsync(signIn, user);
            key = (await users.GetAuthenticatorKeyAsync(user))!;
        }

        var uri = $"otpauth://totp/{Uri.EscapeDataString($"{Issuer}:{user.Email}")}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
        using var qrData = new QRCodeGenerator().CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(qrData).GetGraphic(4);
        // Groups of four, easier to type into an app.
        var grouped = string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(c => new string(c)));
        return TypedResults.Ok(new AuthenticatorSetupResponse(grouped, uri, svg));
    }

    /// <summary>
    /// Turns the authenticator app on, with a code from it. Returns the recovery codes, shown once. Other
    /// browsers are signed out; this one stays signed in.
    /// </summary>
    private static async Task<Results<Ok<RecoveryCodesResponse>, ValidationProblem, UnauthorizedHttpResult>> EnableAppAsync(
        EnableAuthenticatorRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn,
        AccountEmails emails)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (user.TwoFactorEnabled)
            return TypedResults.ValidationProblem(Error("App", "An authenticator app is already set up."));
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, ParentSession.NormalizeCode(request.Code)))
        {
            return TypedResults.ValidationProblem(Error(nameof(request.Code),
                "That code is wrong. Enter the newest code the app shows, and check that the time on your phone is right."));
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, ParentSession.RecoveryCodeCount);
        await ParentSession.SignInAsync(signIn, user);
        await emails.SendTwoFactorChangedAsync(user, "appOn");
        return TypedResults.Ok(new RecoveryCodesResponse([.. codes!]));
    }

    /// <summary>
    /// Removes the authenticator app, so sign-in codes are emailed again. Needs the current password and a code
    /// from the app or a recovery code; wrong attempts count toward the lockout. The key and recovery codes are
    /// discarded, and other browsers are signed out.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> DisableAppAsync(
        [FromBody] DisableAuthenticatorRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users,
        SignInManager<ParentUser> signIn, AccountEmails emails)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (await CurrentPasswordErrorAsync(signIn, user, request.CurrentPassword) is { } passwordError)
            return TypedResults.ValidationProblem(passwordError);
        if (!user.TwoFactorEnabled)
            return TypedResults.ValidationProblem(Error("App", "No authenticator app is set up."));

        if (await CodeErrorAsync(users, user, request.Code, emailed: false,
                "That code is wrong. Enter a code from the app, or one of your recovery codes.") is { } codeError)
            return TypedResults.ValidationProblem(codeError);

        await users.SetTwoFactorEnabledAsync(user, false);
        // A new key, so the old one in the app stops working, and no recovery codes.
        await users.ResetAuthenticatorKeyAsync(user);
        await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
        await ParentSession.SignInAsync(signIn, user);
        await emails.SendTwoFactorChangedAsync(user, "appOff");
        return TypedResults.NoContent();
    }

    /// <summary>New recovery codes, shown once. The old ones stop working.</summary>
    private static async Task<Results<Ok<RecoveryCodesResponse>, ValidationProblem, UnauthorizedHttpResult>> RegenerateRecoveryCodesAsync(
        ClaimsPrincipal principal, UserManager<ParentUser> users, AccountEmails emails)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (!user.TwoFactorEnabled)
            return TypedResults.ValidationProblem(Error("App", "Recovery codes come with an authenticator app. Set one up first."));

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, ParentSession.RecoveryCodeCount);
        await emails.SendTwoFactorChangedAsync(user, "recoveryCodes");
        return TypedResults.Ok(new RecoveryCodesResponse([.. codes!]));
    }

    /// <summary>
    /// Forgets every browser where "remember this browser" was ticked, so they ask for the second step again.
    /// This changes the security stamp, so other browsers are signed out too; this one stays signed in.
    /// </summary>
    private static async Task<Results<NoContent, UnauthorizedHttpResult>> ForgetBrowsersAsync(
        ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();

        await users.UpdateSecurityStampAsync(user);
        await signIn.ForgetTwoFactorClientAsync();
        await ParentSession.SignInAsync(signIn, user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Checks a second-step code from a signed-in parent: from the app or a recovery code when the app is on, and
    /// an emailed code when <paramref name="emailed"/>. Wrong codes count toward the lockout.
    /// </summary>
    internal static async Task<Dictionary<string, string[]>?> CodeErrorAsync(
        UserManager<ParentUser> users, ParentUser user, string? code, bool emailed, string wrongMessage)
    {
        var normalized = ParentSession.NormalizeCode(code);
        var valid = (user.TwoFactorEnabled
                && (await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, normalized)
                    || (await users.RedeemTwoFactorRecoveryCodeAsync(user, (code ?? "").Trim())).Succeeded))
            || (emailed && await users.VerifyTwoFactorTokenAsync(user, EmailCodeTokenProvider.ProviderName, normalized));
        if (valid)
            return null;

        await users.AccessFailedAsync(user);
        return Error("Code", await users.IsLockedOutAsync(user) ? "Too many wrong attempts. Try again later." : wrongMessage);
    }
}
