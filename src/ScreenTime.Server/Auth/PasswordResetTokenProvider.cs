using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Auth;

/// <summary>
/// Password reset links get a shorter life (<see cref="ParentSession.PasswordResetLinkLifespan"/>) than
/// email confirmation links, which use Identity's default provider. Identity reads the system clock for
/// expiry, not the app's <see cref="TimeProvider"/>.
/// </summary>
public class PasswordResetTokenProvider(IDataProtectionProvider dataProtection, ILogger<DataProtectorTokenProvider<ParentUser>> logger)
    : DataProtectorTokenProvider<ParentUser>(dataProtection, Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions
    {
        Name = ProviderName,
        TokenLifespan = ParentSession.PasswordResetLinkLifespan,
    }), logger)
{
    public const string ProviderName = "PasswordReset";
}
