namespace ScreenTime.Server.Data.Entities;

/// <summary>A logical app. <see cref="FamilyId"/> is null for the built-in catalog shared by all families.</summary>
public class AppDefinition
{
    public Guid Id { get; set; }
    public Guid? FamilyId { get; set; }
    public Family? Family { get; set; }
    public required string Name { get; set; }
    public List<AppIdentifier> Identifiers { get; } = [];
}
