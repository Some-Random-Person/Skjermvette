using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Contracts;

public sealed record AppResponse(Guid Id, string Name, bool IsBuiltIn, IReadOnlyList<IdentifierResponse> Identifiers)
{
    /// <summary>Maps an app with the identifiers visible to one family already filtered into <paramref name="identifiers"/>.</summary>
    public static AppResponse From(AppDefinition app, IEnumerable<AppIdentifier> identifiers) => new(
        app.Id,
        app.Name,
        app.FamilyId is null,
        identifiers.OrderBy(i => i.Kind).ThenBy(i => i.Value).Select(IdentifierResponse.From).ToList());
}
