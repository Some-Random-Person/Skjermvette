using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;
using ScreenTime.Server.Services;

namespace ScreenTime.Server.Endpoints;

public static class AuthEndpoints
{
    /// <summary>Rate limit for endpoints that send an email to any address typed in.</summary>
    public const string EmailRateLimit = "email";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization(AppClaims.ParentPolicy);
        group.MapGet("/me", MeAsync).RequireAuthorization(AppClaims.ParentPolicy);
        group.MapPost("/confirm-email", ConfirmEmailAsync);
        group.MapPost("/resend-confirmation", ResendConfirmationAsync).RequireRateLimiting(EmailRateLimit);
        group.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting(EmailRateLimit);
        group.MapPost("/reset-password", ResetPasswordAsync);
    }

    /// <summary>
    /// Creates a parent account with a new family and emails a link to confirm the address.
    /// The parent can sign in once the address is confirmed.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem>> RegisterAsync(
        RegisterRequest request, UserManager<ParentUser> users, AccountEmails emails, AppDbContext db)
    {
        var family = new Family();
        db.Families.Add(family);
        var user = new ParentUser { UserName = request.Email, Email = request.Email, Family = family };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            db.Families.Remove(family);
            return TypedResults.ValidationProblem(ParentSession.Errors(result));
        }

        await emails.SendConfirmationAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Signs a parent in with a persistent cookie. A right password on an unconfirmed address is 403,
    /// so the dashboard can offer to send the confirmation link again.
    /// </summary>
    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, UserManager<ParentUser> users, SignInManager<ParentUser> signIn)
    {
        var user = await users.FindByEmailAsync(request.Email);
        var check = user is null
            ? SignInResult.Failed
            : await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return TypedResults.Problem(
                check.IsLockedOut ? "Too many failed attempts. Try again later." : "Wrong email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }
        // Checked after the password, so this doesn't tell anyone else whether an address is confirmed.
        if (!user!.EmailConfirmed)
        {
            return TypedResults.Problem(
                "Confirm your email address first: open the link we sent you.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        await ParentSession.SignInAsync(signIn, user);
        return TypedResults.Ok(MeResponse.From(user));
    }

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
        ConfirmEmailRequest request, UserManager<ParentUser> users)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || AccountEmails.DecodeToken(request.Token) is not { } token)
            return TypedResults.ValidationProblem(InvalidLink);

        var result = await users.ConfirmEmailAsync(user, token);
        return result.Succeeded ? TypedResults.NoContent() : TypedResults.ValidationProblem(LinkErrors(result));
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
    /// sent to it. The parent then signs in with the new password.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem>> ResetPasswordAsync(
        ResetPasswordRequest request, UserManager<ParentUser> users)
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
