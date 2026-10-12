using Microsoft.AspNetCore.Identity;

namespace ScreenTime.Server.Data.Entities;

public class ParentUser : IdentityUser<Guid>
{
    public Guid FamilyId { get; set; }
    public Family Family { get; set; } = null!;
}
