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

    /// <summary>How long an email confirmation link works. The email says so (<c>emails.en.json</c>).</summary>
    public static readonly TimeSpan ConfirmationLinkLifespan = TimeSpan.FromHours(24);

    /// <summary>How long a password reset link works. The email says so (<c>emails.en.json</c>).</summary>
    public static readonly TimeSpan PasswordResetLinkLifespan = TimeSpan.FromHours(1);

    /// <summary>How long a sign-in code sent by email works. The email says so (<c>emails.en.json</c>).</summary>
    public static readonly TimeSpan EmailCodeLifespan = TimeSpan.FromMinutes(10);

    /// <summary>How long a parent has, after the right password, to enter the second-step code.</summary>
    public static readonly TimeSpan SecondStepLifespan = TimeSpan.FromMinutes(15);

    /// <summary>How long "remember this browser" skips the second step. The dashboard says so (<c>en.json</c>).</summary>
    public static readonly TimeSpan RememberBrowserLifespan = TimeSpan.FromDays(30);

    /// <summary>How many recovery codes a parent gets with the authenticator app.</summary>
    public const int RecoveryCodeCount = 10;

    /// <summary>A code from an authenticator app or an email, without the spaces or dashes people type.</summary>
    public static string NormalizeCode(string? code) => (code ?? "").Replace(" ", "").Replace("-", "");

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
