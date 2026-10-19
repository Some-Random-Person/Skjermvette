using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Auth;

/// <summary>
/// Sign-in codes sent by email: 6 random digits, single use, valid for <see cref="ParentSession.EmailCodeLifespan"/>.
/// Only the newest code works. Its hash and expiry are kept with the parent's other Identity tokens.
/// Replaces Identity's own email provider, whose codes can't be made single use.
/// </summary>
public class EmailCodeTokenProvider(TimeProvider time) : IUserTwoFactorTokenProvider<ParentUser>
{
    /// <summary>The name of Identity's own email provider (<see cref="TokenOptions.DefaultEmailProvider"/>), which this replaces.</summary>
    public const string ProviderName = "Email";

    private const string LoginProvider = "[Skjermvette]";
    private const string TokenName = "EmailCode";

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ParentUser> manager, ParentUser user) =>
        Task.FromResult(user.EmailConfirmed);

    public async Task<string> GenerateAsync(string purpose, UserManager<ParentUser> manager, ParentUser user)
    {
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var expires = (time.GetUtcNow() + ParentSession.EmailCodeLifespan).ToUnixTimeSeconds();
        await manager.SetAuthenticationTokenAsync(user, LoginProvider, TokenName, $"{Hash(code)}|{expires}");
        return code;
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<ParentUser> manager, ParentUser user)
    {
        var stored = await manager.GetAuthenticationTokenAsync(user, LoginProvider, TokenName);
        if (stored?.Split('|') is not [var hash, var expires]
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(Hash(token))))
            return false;

        await manager.RemoveAuthenticationTokenAsync(user, LoginProvider, TokenName);
        return time.GetUtcNow().ToUnixTimeSeconds() <= long.Parse(expires, CultureInfo.InvariantCulture);
    }

    private static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
