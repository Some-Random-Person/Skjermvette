using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Skjermvette.Server.Auth;
using Skjermvette.Server.Contracts;
using Skjermvette.Server.Data;
using Skjermvette.Server.Data.Entities;
using Skjermvette.Server.Services;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Skjermvette.Server.Endpoints;

public static class AuthEndpoints
{
    /// <summary>Rate limit for endpoints that send an email to any address typed in.</summary>
    public const string EmailRateLimit = "email";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");
        group.MapPost("/register", RegisterAsync).RequireRateLimiting(EmailRateLimit);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/login/code", LoginCodeAsync);
        group.MapPost("/login/email-code", SendLoginCodeAsync).RequireRateLimiting(EmailRateLimit);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization(AppClaims.ParentPolicy);
        group.MapGet("/me", MeAsync).RequireAuthorization(AppClaims.ParentPolicy);
        group.MapPost("/confirm-email", ConfirmEmailAsync);
        group.MapPost("/confirm-email-change", ConfirmEmailChangeAsync);
        group.MapPost("/resend-confirmation", ResendConfirmationAsync).RequireRateLimiting(EmailRateLimit);
        group.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting(EmailRateLimit);
        group.MapPost("/reset-password", ResetPasswordAsync);
    }

    /// <summary>
    /// Creates a parent account with a new family and emails a link to confirm the address.
    /// The parent can sign in once the address is confirmed. If the address already has an account,
    /// the reply is the same and that address is emailed instead, so it doesn't reveal which addresses
    /// have accounts. Rate limited.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem>> RegisterAsync(
        RegisterRequest request, UserManager<ParentUser> users, AccountEmails emails, AppDbContext db)
    {
        var family = new Family();
        db.Families.Add(family);
        var user = new ParentUser { UserName = request.Email, Email = request.Email, Family = family };

        // Identity checks and hashes the password before it checks the address, so a taken address
        // gets the same password errors, and takes about as long, as a new one.
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            db.Families.Remove(family);
            if (!result.Errors.All(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                    or nameof(IdentityErrorDescriber.DuplicateUserName)))
                return TypedResults.ValidationProblem(ParentSession.Errors(result));

            if (await users.FindByEmailAsync(request.Email) is { } existing)
                emails.SendRegisterAttempt(existing);
            return TypedResults.NoContent();
        }

        await emails.SendConfirmationAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// First sign-in step: checks the password. On a browser remembered for this parent, signs in with a
    /// persistent cookie (<c>Me</c>). Otherwise answers which second step is needed (<c>SecondStep</c>): a code
    /// from the authenticator app, or a code that's emailed now; then <c>/api/auth/login/code</c> finishes the
    /// sign-in. A right password on an unconfirmed address is 403, so the dashboard can offer to send the
    /// confirmation link again.
    /// </summary>
    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, UserManager<ParentUser> users, ParentSignInManager signIn, AccountEmails emails)
    {
        var user = await users.FindByEmailAsync(request.Email);
        var check = user is null
            ? SignInResult.Failed
            : await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return TypedResults.Problem(
                check.IsLockedOut ? LockedOutMessage : "Wrong email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }
        // Checked after the password, so this doesn't tell anyone else whether an address is confirmed.
        if (!user!.EmailConfirmed)
        {
            return TypedResults.Problem(
                "Confirm your email address first: open the link we sent you.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if ((await signIn.SignInOrTwoFactorAsync(user)).Succeeded)
            return TypedResults.Ok(new LoginResponse(MeResponse.From(user), null));
        if (user.TwoFactorEnabled)
            return TypedResults.Ok(new LoginResponse(null, TwoFactorMethodEnum.App));

        await emails.SendCodeAsync(user);
        return TypedResults.Ok(new LoginResponse(null, TwoFactorMethodEnum.Email));
    }

    /// <summary>
    /// Second sign-in step: a code from the authenticator app, an emailed code, or a recovery code (only when the
    /// app is set up). Wrong codes count toward the lockout. <c>RememberBrowser</c> skips this step on this browser
    /// for 30 days (not with a recovery code). Using a recovery code signs other browsers out.
    /// </summary>
    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> LoginCodeAsync(
        LoginCodeRequest request, UserManager<ParentUser> users, ParentSignInManager signIn)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is not { } user)
            return SecondStepExpired;

        var code = ParentSession.NormalizeCode(request.Code);
        var result = request.Method switch
        {
            TwoFactorMethodEnum.App when user.TwoFactorEnabled =>
                await signIn.TwoFactorAuthenticatorSignInAsync(code, isPersistent: true, request.RememberBrowser),
            TwoFactorMethodEnum.Email =>
                await signIn.TwoFactorSignInAsync(EmailCodeTokenProvider.ProviderName, code, isPersistent: true, request.RememberBrowser),
            TwoFactorMethodEnum.RecoveryCode when user.TwoFactorEnabled =>
                await RecoveryCodeSignInAsync(users, signIn, user, request.Code),
            _ => SignInResult.Failed,
        };
        if (result.Succeeded)
            return TypedResults.Ok(MeResponse.From(user));
        return TypedResults.Problem(
            result.IsLockedOut ? LockedOutMessage : "That code is wrong or has expired.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Emails a new sign-in code, after the right password (<c>/api/auth/login</c>): to send it again, or instead
    /// of the authenticator app. Rate limited.
    /// </summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> SendLoginCodeAsync(ParentSignInManager signIn, AccountEmails emails)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is not { } user)
            return SecondStepExpired;

        await emails.SendCodeAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Identity doesn't count wrong recovery codes toward the lockout, or change the security stamp when one is
    /// used, so this does both. The sign-in is made persistent like any other.
    /// </summary>
    private static async Task<SignInResult> RecoveryCodeSignInAsync(
        UserManager<ParentUser> users, ParentSignInManager signIn, ParentUser user, string? code)
    {
        if (await users.IsLockedOutAsync(user))
            return SignInResult.LockedOut;

        if (!(await signIn.TwoFactorRecoveryCodeSignInAsync((code ?? "").Trim())).Succeeded)
        {
            await users.AccessFailedAsync(user);
            return await users.IsLockedOutAsync(user) ? SignInResult.LockedOut : SignInResult.Failed;
        }

        // A recovery code means the phone may be lost: sign other browsers out.
        await users.UpdateSecurityStampAsync(user);
        await ParentSession.SignInAsync(signIn, user);
        return SignInResult.Success;
    }

    private const string LockedOutMessage = "Too many failed attempts. Try again later.";

    private static ProblemHttpResult SecondStepExpired => TypedResults.Problem(
        "Your sign-in timed out. Enter your email and password again.", statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>Signs the parent out.</summary>
    private static async Task<NoContent> LogoutAsync(SignInManager<ParentUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    /// <summary>The signed-in parent, read from the database so name and email changes show straight away.</summary>
    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> MeAsync(
        ClaimsPrincipal principal, UserManager<ParentUser> users) =>
        await users.GetUserAsync(principal) is { } user
            ? TypedResults.Ok(MeResponse.From(user))
            : TypedResults.Unauthorized();

    /// <summary>Confirms a parent's email address with the token from the link in the confirmation email.</summary>
    private static async Task<Results<NoContent, ValidationProblem>> ConfirmEmailAsync(
        ConfirmEmailRequest request, UserManager<ParentUser> users, AppDbContext db)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || AccountEmails.DecodeToken(request.Token) is not { } token)
            return TypedResults.ValidationProblem(InvalidLink);
        // Opening the link twice (or two requests at once) isn't an error.
        if (user.EmailConfirmed)
            return TypedResults.NoContent();

        var result = await users.ConfirmEmailAsync(user, token);
        // A request at the same moment may have confirmed it first (#49).
        return result.Succeeded || await ReloadAsync(db, user) is { EmailConfirmed: true }
            ? TypedResults.NoContent()
            : TypedResults.ValidationProblem(LinkErrors(result));
    }

    /// <summary>
    /// Changes a parent's email to the new address, with the token from the link emailed to it. Other browsers
    /// are signed out; if this browser is signed in as the same parent, it stays signed in.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem>> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn,
        AppDbContext db)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || AccountEmails.DecodeToken(request.Token) is not { } token)
            return TypedResults.ValidationProblem(InvalidLink);

        var email = (request.Email ?? "").Trim();
        // Opening the link twice isn't an error, though the first one changed the security stamp the token depends on.
        if (user.NormalizedEmail == users.NormalizeEmail(email))
            return TypedResults.NoContent();
        // Checks the token, marks the address as confirmed, and validates it (e.g. taken in the meantime).
        var result = await users.ChangeEmailAsync(user, email, token);
        if (result.Succeeded)
            result = await users.SetUserNameAsync(user, email);
        if (!result.Succeeded)
        {
            // A request at the same moment may have changed it first (#49).
            if (await ReloadAsync(db, user) is { } current && current.NormalizedEmail == users.NormalizeEmail(email))
                return TypedResults.NoContent();
            return TypedResults.ValidationProblem(LinkErrors(result));
        }

        if (users.GetUserId(principal) == user.Id.ToString())
            await ParentSession.SignInAsync(signIn, user);
        return TypedResults.NoContent();
    }

    /// <summary>The user as saved now, after a failed update left the tracked copy stale.</summary>
    private static async Task<ParentUser> ReloadAsync(AppDbContext db, ParentUser user)
    {
        await db.Entry(user).ReloadAsync();
        return user;
    }

    /// <summary>
    /// Sends the confirmation link again if the address has an unconfirmed account. The reply is the same
    /// either way, so it doesn't reveal which addresses have accounts. Rate limited.
    /// </summary>
    private static async Task<NoContent> ResendConfirmationAsync(
        EmailRequest request, UserManager<ParentUser> users, AccountEmails emails)
    {
        if (await users.FindByEmailAsync(request.Email ?? "") is { EmailConfirmed: false } user)
            await emails.SendConfirmationAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Emails a password reset link if the address has an account. The reply is the same either way,
    /// so it doesn't reveal which addresses have accounts. Rate limited.
    /// </summary>
    private static async Task<NoContent> ForgotPasswordAsync(
        EmailRequest request, UserManager<ParentUser> users, AccountEmails emails)
    {
        if (await users.FindByEmailAsync(request.Email ?? "") is { } user)
            await emails.SendPasswordResetAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Sets a new password with the token from the link in the reset email. Other browsers are signed out,
    /// a lockout from wrong passwords is lifted, and the address counts as confirmed, since the link was
    /// sent to it. The parent then signs in with the new password, and gets an email about the change.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem>> ResetPasswordAsync(
        ResetPasswordRequest request, UserManager<ParentUser> users, AccountEmails emails)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || AccountEmails.DecodeToken(request.Token) is not { } token)
            return TypedResults.ValidationProblem(InvalidLink);

        // Also changes the security stamp, which signs other browsers out and makes the link single-use.
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword ?? "");
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(LinkErrors(result));

        user.EmailConfirmed = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await users.UpdateAsync(user);
        await emails.SendPasswordChangedAsync(user);
        return TypedResults.NoContent();
    }

    private static Dictionary<string, string[]> InvalidLink =>
        new() { ["Token"] = ["This link has expired or was already used. Ask for a new one."] };

    /// <summary>Identity's errors, with its "Invalid token." replaced by something a parent can act on.</summary>
    private static Dictionary<string, string[]> LinkErrors(IdentityResult result) =>
        result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
            ? InvalidLink
            : ParentSession.Errors(result);
}
