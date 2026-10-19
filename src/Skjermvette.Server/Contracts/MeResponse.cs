using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Contracts;

public sealed record MeResponse(string Email, string? Name, Guid FamilyId)
{
    public static MeResponse From(ParentUser user) => new(user.Email!, user.DisplayName, user.FamilyId);
}
