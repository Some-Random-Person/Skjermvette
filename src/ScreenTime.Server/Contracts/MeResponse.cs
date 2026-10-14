using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Contracts;

public sealed record MeResponse(string Email, string? Name, Guid FamilyId)
{
    public static MeResponse From(ParentUser user) => new(user.Email!, user.DisplayName, user.FamilyId);
}
