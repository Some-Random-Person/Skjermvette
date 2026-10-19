using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skjermvette.Server.Data;

namespace Skjermvette.Server.Auth;

/// <summary>
/// Authenticates devices by the token they got when pairing, sent as <c>Authorization: Bearer</c>,
/// or as the <c>access_token</c> query parameter for SignalR WebSockets.
/// </summary>
public class DeviceTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceToken";

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken();
        if (token is null)
            return AuthenticateResult.NoResult();

        var hash = Hash(token);
        var device = await db.Devices.AsNoTracking()
            .Where(d => d.TokenHash == hash && d.UnpairedAtUtc == null)
            .Select(d => new { d.Id, d.ChildId, d.Child.FamilyId })
            .FirstOrDefaultAsync(Context.RequestAborted);
        if (device is null)
            return AuthenticateResult.Fail("Unknown device token.");

        var identity = new ClaimsIdentity(
        [
            new Claim(AppClaims.DeviceId, device.Id.ToString()),
            new Claim(AppClaims.ChildId, device.ChildId.ToString()),
            new Claim(AppClaims.FamilyId, device.FamilyId.ToString()),
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    private string? ReadToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header["Bearer ".Length..].Trim();

        return Request.Path.StartsWithSegments("/hubs") ? Request.Query["access_token"].FirstOrDefault() : null;
    }
}
