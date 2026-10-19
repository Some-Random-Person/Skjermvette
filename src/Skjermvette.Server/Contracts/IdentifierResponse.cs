using Skjermvette.Core.Models;
using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Contracts;

/// <summary><see cref="IsBuiltIn"/> identifiers belong to the shared catalog and can't be removed by a family.</summary>
public sealed record IdentifierResponse(Guid Id, MatcherKindEnum Kind, string Value, bool IsBuiltIn)
{
    public static IdentifierResponse From(AppIdentifier identifier) =>
        new(identifier.Id, identifier.Kind, identifier.Value, identifier.FamilyId is null);
}
