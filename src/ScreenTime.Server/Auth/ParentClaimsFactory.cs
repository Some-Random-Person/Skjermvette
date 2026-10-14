using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Auth;

/// <summary>
/// Adds the parent's family to every sign-in Identity creates, including the one rebuilt when the
/// sign-in is re-checked against the database, so the family claim never goes missing.
/// </summary>
public class ParentClaimsFactory(UserManager<ParentUser> users, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ParentUser>(users, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ParentUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaims.FamilyId, user.FamilyId.ToString()));
        return identity;
    }
}
