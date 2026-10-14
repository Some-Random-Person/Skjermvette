using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Endpoints;

/// <summary>The signed-in parent's own account: name, email and password.</summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account").WithTags("Account").RequireAuthorization(AppClaims.ParentPolicy);
        group.MapPut("/name", UpdateNameAsync);
        group.MapPut("/email", ChangeEmailAsync);
        group.MapPut("/password", ChangePasswordAsync);
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
