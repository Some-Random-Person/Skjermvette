using Microsoft.EntityFrameworkCore;
using Skjermvette.Core.Models;
using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Data;

public static class CatalogQueries
{
    /// <summary>Built-in apps plus the family's own, each with built-in identifiers plus the family's own.</summary>
    public static Task<List<AppDefinition>> VisibleAppsAsync(this AppDbContext db, Guid familyId, CancellationToken ct) =>
        db.AppDefinitions.AsNoTracking()
            .Where(a => a.FamilyId == null || a.FamilyId == familyId)
            .Include(a => a.Identifiers.Where(i => i.FamilyId == null || i.FamilyId == familyId))
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

    /// <summary>
    /// The family's catalog for matching identifiers to apps. When a family has mapped an identifier itself,
    /// that mapping wins over a built-in one with the same value, so catalog updates never change how
    /// a family's existing mappings count.
    /// </summary>
    public static List<LogicalApp> ToCoreCatalog(this IReadOnlyCollection<AppDefinition> apps)
    {
        var familyKeys = apps
            .SelectMany(a => a.Identifiers)
            .Where(i => i.FamilyId is not null)
            .Select(Key)
            .ToHashSet();

        return apps
            .Select(app => new LogicalApp(
                app.Id,
                app.Name,
                app.Identifiers
                    .Where(i => i.FamilyId is not null || !familyKeys.Contains(Key(i)))
                    .Select(i => new AppMatcher(i.Kind, i.Value))
                    .ToList()))
            .ToList();
    }

    private static (MatcherKindEnum, string) Key(AppIdentifier identifier) =>
        (identifier.Kind, identifier.Kind == MatcherKindEnum.IosToken ? identifier.Value : identifier.Value.ToLowerInvariant());
}
