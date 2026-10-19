using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skjermvette.Server.Auth;
using Skjermvette.Server.Contracts;
using Skjermvette.Server.Data;
using Skjermvette.Server.Data.Entities;
using Skjermvette.Server.Services;

namespace Skjermvette.Server.Endpoints;

/// <summary>The signed-in parent's own account: name, email, password and deleting it.</summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account").WithTags("Account").RequireAuthorization(AppClaims.ParentPolicy);
        group.MapPut("/name", UpdateNameAsync);
        group.MapPut("/email", ChangeEmailAsync);
        group.MapPut("/password", ChangePasswordAsync);
        group.MapGet("/deletion", GetDeletionAsync);
        group.MapPost("/deletion/email-code", SendDeletionCodeAsync).RequireRateLimiting(AuthEndpoints.EmailRateLimit);
        group.MapDelete("/", DeleteAsync);
    }

    /// <summary>Sets or clears the parent's name. Without a name, the dashboard shows the email.</summary>
    private static async Task<Results<Ok<MeResponse>, ValidationProblem, UnauthorizedHttpResult>> UpdateNameAsync(
        UpdateNameRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users)
    {
        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        if (name is { Length: > 100 })
            return TypedResults.ValidationProblem(Error(nameof(request.Name), "Name must be at most 100 characters."));

        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();

        user.DisplayName = name;
        var result = await users.UpdateAsync(user);
        return result.Succeeded
            ? TypedResults.Ok(MeResponse.From(user))
            : TypedResults.ValidationProblem(ParentSession.Errors(result));
    }

    /// <summary>
    /// Asks to change the email used to sign in. Needs the current password. Nothing changes until the link
    /// emailed to the new address is opened (<c>/api/auth/confirm-email-change</c>); the current address is
    /// told about the request.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> ChangeEmailAsync(
        ChangeEmailRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn,
        AccountEmails emails)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (await CurrentPasswordErrorAsync(signIn, user, request.CurrentPassword) is { } passwordError)
            return TypedResults.ValidationProblem(passwordError);

        // The same checks Identity makes when the change is confirmed, so mistakes show up now.
        var email = (request.NewEmail ?? "").Trim();
        if (!new EmailAddressAttribute().IsValid(email))
            return TypedResults.ValidationProblem(ParentSession.Errors(IdentityResult.Failed(users.ErrorDescriber.InvalidEmail(email))));
        if (users.NormalizeEmail(email) == user.NormalizedEmail)
            return TypedResults.ValidationProblem(Error(nameof(request.NewEmail), "That's already your email."));
        if (await users.FindByEmailAsync(email) is not null)
            return TypedResults.ValidationProblem(ParentSession.Errors(IdentityResult.Failed(users.ErrorDescriber.DuplicateEmail(email))));

        await emails.SendEmailChangeAsync(user, email);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Changes the password. Needs the current password; this browser stays signed in, and the parent gets
    /// an email about it, with a link to reset the password if it wasn't them.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn,
        AccountEmails emails)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (await CurrentPasswordErrorAsync(signIn, user, request.CurrentPassword) is { } passwordError)
            return TypedResults.ValidationProblem(passwordError);

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword ?? "");
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(ParentSession.Errors(result));

        await ParentSession.SignInAsync(signIn, user);
        await emails.SendPasswordChangedAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// What deleting the account would remove, so the dashboard can warn the last parent that the family,
    /// its children and their devices go too.
    /// </summary>
    private static async Task<Ok<AccountDeletionResponse>> GetDeletionAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var familyId = user.GetFamilyId();
        var parents = await db.Users.CountAsync(u => u.FamilyId == familyId, ct);
        var children = await db.Children.CountAsync(c => c.FamilyId == familyId, ct);
        var devices = await db.Devices.CountAsync(d => d.Child.FamilyId == familyId && d.UnpairedAtUtc == null, ct);
        return TypedResults.Ok(new AccountDeletionResponse(parents <= 1, children, devices));
    }

    /// <summary>
    /// Emails a code to confirm deleting the account (<c>DELETE /api/account</c>), for parents without the
    /// authenticator app at hand. Rate limited.
    /// </summary>
    private static async Task<Results<NoContent, UnauthorizedHttpResult>> SendDeletionCodeAsync(
        ClaimsPrincipal principal, UserManager<ParentUser> users, AccountEmails emails)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();

        await emails.SendCodeAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes the parent's account and signs out. Needs the current password and a second-step code: from the
    /// authenticator app, a recovery code, or an emailed one (<c>POST /api/account/deletion/email-code</c>). Wrong
    /// attempts count toward the lockout. If no other parent is left, the family is deleted too: children,
    /// devices, usage history, rules and the family's own apps.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> DeleteAsync(
        [FromBody] DeleteAccountRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users,
        SignInManager<ParentUser> signIn, AppDbContext db, CancellationToken ct)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (await CurrentPasswordErrorAsync(signIn, user, request.CurrentPassword) is { } passwordError)
            return TypedResults.ValidationProblem(passwordError);
        if (await TwoFactorEndpoints.CodeErrorAsync(users, user, request.Code, emailed: true,
                "That code is wrong or has expired. Enter a code from your authenticator app, a recovery code, or the code we emailed you.") is { } codeError)
            return TypedResults.ValidationProblem(codeError);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(ParentSession.Errors(result));

        if (!await db.Users.AnyAsync(u => u.FamilyId == user.FamilyId, ct))
        {
            // The family's own apps don't cascade from the family, so they go first (with their identifiers and rules).
            await db.AppDefinitions.Where(a => a.FamilyId == user.FamilyId).ExecuteDeleteAsync(ct);
            // The database cascades the rest: children with their devices, leases, usage, rules, pairing codes
            // and extra time, and the identifiers the family added to built-in apps.
            await db.Families.Where(f => f.Id == user.FamilyId).ExecuteDeleteAsync(ct);
        }
        await transaction.CommitAsync(ct);

        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Checks the current password. Wrong attempts count toward the same lockout as signing in,
    /// so the password can't be guessed here instead.
    /// </summary>
    internal static async Task<Dictionary<string, string[]>?> CurrentPasswordErrorAsync(
        SignInManager<ParentUser> signIn, ParentUser user, string? password)
    {
        var result = await signIn.CheckPasswordSignInAsync(user, password ?? "", lockoutOnFailure: true);
        if (result.Succeeded)
            return null;
        return Error("CurrentPassword", result.IsLockedOut
            ? "Too many wrong attempts. Try again later."
            : "The current password is wrong.");
    }

    internal static Dictionary<string, string[]> Error(string field, string message) => new() { [field] = [message] };
}
