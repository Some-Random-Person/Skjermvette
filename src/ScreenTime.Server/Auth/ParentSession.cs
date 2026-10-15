using Microsoft.AspNetCore.Identity;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Auth;

public static class ParentSession
{
    /// <summary>
    /// How often a parent's sign-in is re-checked against the database. A changed password or email, or a
    /// deleted account, signs other browsers out within this time.
    /// </summary>
    public static readonly TimeSpan RecheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>PBKDF2-HMAC-SHA512 iterations for parent passwords (above OWASP's 210,000 minimum).</summary>
    public const int PasswordHashIterations = 250_000;

    /// <summary>
    /// Signs a parent in (or refreshes their cookie after an account change) with a persistent cookie.
    /// The family claim comes from <see cref="ParentClaimsFactory"/>.
    /// </summary>
    public static Task SignInAsync(SignInManager<ParentUser> signIn, ParentUser user) =>
        signIn.SignInAsync(user, isPersistent: true);

    /// <summary>Identity's errors as validation errors, keyed by error code.</summary>
    public static Dictionary<string, string[]> Errors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
