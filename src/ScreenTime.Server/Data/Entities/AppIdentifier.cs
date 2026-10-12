using ScreenTime.Core.Models;

namespace ScreenTime.Server.Data.Entities;

public class AppIdentifier
{
    public Guid Id { get; set; }
    public Guid AppDefinitionId { get; set; }
    public AppDefinition AppDefinition { get; set; } = null!;

    /// <summary>The family that added this identifier, or null for the built-in catalog.</summary>
    public Guid? FamilyId { get; set; }

    public MatcherKindEnum Kind { get; set; }
    public required string Value { get; set; }
}
