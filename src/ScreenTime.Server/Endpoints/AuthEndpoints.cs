using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization(AppClaims.ParentPolicy);
        group.MapGet("/me", MeAsync).RequireAuthorization(AppClaims.ParentPolicy);
    }

    /// <summary>Creates a parent account with a new family and signs in.</summary>
    private static async Task<Results<Ok<MeResponse>, ValidationProblem>> RegisterAsync(
        RegisterRequest request, UserManager<ParentUser> users, SignInManager<ParentUser> signIn, AppDbContext db)
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

        await ParentSession.SignInAsync(signIn, user);
        return TypedResults.Ok(MeResponse.From(user));
    }

    /// <summary>Signs a parent in with a persistent cookie.</summary>
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

        await ParentSession.SignInAsync(signIn, user!);
        return TypedResults.Ok(MeResponse.From(user!));
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
}
