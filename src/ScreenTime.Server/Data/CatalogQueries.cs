using Microsoft.EntityFrameworkCore;
using ScreenTime.Core.Models;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Data;

public static class CatalogQueries
{
    /// <summary>Built-in apps plus the family's own, each with built-in identifiers plus the family's own.</summary>
    public static Task<List<AppDefinition>> VisibleAppsAsync(this AppDbContext db, Guid familyId, CancellationToken ct) =>
        db.AppDefinitions.AsNoTracking()
            .Where(a => a.FamilyId == null || a.FamilyId == familyId)
            .Include(a => a.Identifiers.Where(i => i.FamilyId == null || i.FamilyId == familyId))
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

    public static LogicalApp ToCore(this AppDefinition app) =>
        new(app.Id, app.Name, app.Identifiers.Select(i => new AppMatcher(i.Kind, i.Value)).ToList());
}
