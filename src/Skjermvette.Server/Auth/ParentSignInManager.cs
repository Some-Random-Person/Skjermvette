using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Auth;

/// <summary>
/// Every parent signs in with a second step: the authenticator app when it's set up
/// (<see cref="IdentityUser{TKey}.TwoFactorEnabled"/>), otherwise a code sent by email.
/// </summary>
public class ParentSignInManager(
    UserManager<ParentUser> users,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ParentUser> claimsFactory,
    IOptions<IdentityOptions> options,
    ILogger<SignInManager<ParentUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ParentUser> confirmation)
    : SignInManager<ParentUser>(users, contextAccessor, claimsFactory, options, logger, schemes, confirmation)
{
    /// <summary>
    /// Call after the password and email confirmation are checked. Signs in on a browser remembered for this
    /// parent; otherwise stores the parent in the short-lived two-factor cookie and returns
    /// <see cref="SignInResult.RequiresTwoFactor"/>.
    /// </summary>
    public Task<SignInResult> SignInOrTwoFactorAsync(ParentUser user) => SignInOrTwoFactorAsync(user, isPersistent: true);

    public override Task<bool> IsTwoFactorEnabledAsync(ParentUser user) => Task.FromResult(true);
}
