using System.Security.Claims;

namespace Skjermvette.Server.Auth;

public static class AppClaims
{
    public const string FamilyId = "family_id";
    public const string DeviceId = "device_id";
    public const string ChildId = "child_id";

    public const string ParentPolicy = "Parent";
    public const string DevicePolicy = "Device";

    public static Guid GetFamilyId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(FamilyId)!);
    public static Guid GetDeviceId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(DeviceId)!);
    public static Guid GetChildId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ChildId)!);
}
