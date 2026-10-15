using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Endpoints;

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
    /// Changes the email used to sign in. Needs the current password. There's no confirmation email
    /// (the server doesn't send email), so the dashboard asks for the new address twice.
    /// </summary>
    private static async Task<Results<Ok<MeResponse>, ValidationProblem, UnauthorizedHttpResult>> ChangeEmailAsync(
        ChangeEmailRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (await CurrentPasswordErrorAsync(signIn, user, request.CurrentPassword) is { } passwordError)
            return TypedResults.ValidationProblem(passwordError);

        var email = (request.NewEmail ?? "").Trim();
        user.Email = email;
        user.UserName = email;
        // UpdateAsync validates the address and that no other account uses it.
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(ParentSession.Errors(result));

        await users.UpdateSecurityStampAsync(user);
        await ParentSession.SignInAsync(signIn, user);
        return TypedResults.Ok(MeResponse.From(user));
    }

    /// <summary>Changes the password. Needs the current password; this browser stays signed in.</summary>
    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users, SignInManager<ParentUser> signIn)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (await CurrentPasswordErrorAsync(signIn, user, request.CurrentPassword) is { } passwordError)
            return TypedResults.ValidationProblem(passwordError);

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword ?? "");
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(ParentSession.Errors(result));

        await ParentSession.SignInAsync(signIn, user);
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
    /// Deletes the parent's account and signs out. Needs the current password. If no other parent is left,
    /// the family is deleted too: children, devices, usage history, rules and the family's own apps.
    /// </summary>
    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> DeleteAsync(
        [FromBody] DeleteAccountRequest request, ClaimsPrincipal principal, UserManager<ParentUser> users,
        SignInManager<ParentUser> signIn, AppDbContext db, CancellationToken ct)
    {
        if (await users.GetUserAsync(principal) is not { } user)
            return TypedResults.Unauthorized();
        if (await CurrentPasswordErrorAsync(signIn, user, request.CurrentPassword) is { } passwordError)
            return TypedResults.ValidationProblem(passwordError);

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
    private static async Task<Dictionary<string, string[]>?> CurrentPasswordErrorAsync(
        SignInManager<ParentUser> signIn, ParentUser user, string? password)
    {
        var result = await signIn.CheckPasswordSignInAsync(user, password ?? "", lockoutOnFailure: true);
        if (result.Succeeded)
            return null;
        return Error("CurrentPassword", result.IsLockedOut
            ? "Too many wrong attempts. Try again later."
            : "The current password is wrong.");
    }

    private static Dictionary<string, string[]> Error(string field, string message) => new() { [field] = [message] };
}
