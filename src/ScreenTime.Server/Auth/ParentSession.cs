using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Auth;

public static class ParentSession
{
    /// <summary>Signs a parent in (or refreshes their cookie after an account change) with a persistent cookie.</summary>
    public static Task SignInAsync(SignInManager<ParentUser> signIn, ParentUser user) =>
        signIn.SignInWithClaimsAsync(user, isPersistent: true, [new Claim(AppClaims.FamilyId, user.FamilyId.ToString())]);

    /// <summary>Identity's errors as validation errors, keyed by error code.</summary>
    public static Dictionary<string, string[]> Errors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
