using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skjermvette.Core;
using Skjermvette.Core.Models;
using Skjermvette.Server.Auth;
using Skjermvette.Server.Contracts;
using Skjermvette.Server.Data;
using Skjermvette.Server.Data.Entities;
using Skjermvette.Server.Services;

namespace Skjermvette.Server.Endpoints;

public static class AppEndpoints
{
    public static void MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/apps").WithTags("Apps").RequireAuthorization(AppClaims.ParentPolicy);
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", RenameAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/identifiers", AddIdentifierAsync);
        group.MapDelete("/{id:guid}/identifiers/{identifierId:guid}", RemoveIdentifierAsync);
        group.MapGet("/unassigned", UnassignedAsync);
    }

    /// <summary>The built-in catalog plus the family's own apps and identifiers.</summary>
    private static async Task<Ok<List<AppResponse>>> ListAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var apps = await db.VisibleAppsAsync(user.GetFamilyId(), ct);
        return TypedResults.Ok(apps.Select(a => AppResponse.From(a, a.Identifiers)).ToList());
    }

    /// <summary>Adds a family-specific app. Identifiers are added separately.</summary>
    private static async Task<Results<Created<AppResponse>, ValidationProblem>> CreateAsync(
        AppRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (NameError(request) is { } error)
            return error;

        var app = new AppDefinition { FamilyId = user.GetFamilyId(), Name = request.Name.Trim() };
        db.AppDefinitions.Add(app);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/apps/{app.Id}", AppResponse.From(app, []));
    }

    /// <summary>Renames a family-specific app. Built-in apps can't be renamed.</summary>
    private static async Task<Results<NoContent, NotFound, ValidationProblem>> RenameAsync(
        Guid id, AppRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (NameError(request) is { } error)
            return error;

        var app = await db.AppDefinitions.FirstOrDefaultAsync(a => a.Id == id && a.FamilyId == user.GetFamilyId(), ct);
        if (app is null)
            return TypedResults.NotFound();

        app.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a family-specific app and any rules for it. Its past usage stays, but counts as
    /// "other apps" from then on. Built-in apps can't be deleted.
    /// </summary>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, BudgetService budget, CancellationToken ct)
    {
        var familyId = user.GetFamilyId();
        var app = await db.AppDefinitions.FirstOrDefaultAsync(a => a.Id == id && a.FamilyId == familyId, ct);
        if (app is null)
            return TypedResults.NotFound();

        var affectedChildren = await db.AppRules.Where(r => r.AppDefinitionId == id).Select(r => r.ChildId).ToListAsync(ct);
        db.AppDefinitions.Remove(app);
        await db.SaveChangesAsync(ct);

        foreach (var childId in affectedChildren)
            await budget.PushAsync(childId, ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Maps an identifier (process, package, bundle, iOS token or domain) to an app for this family.
    /// Usage already recorded under that identifier is reassigned, and devices get the new budget.
    /// </summary>
    private static async Task<Results<Created<IdentifierResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> AddIdentifierAsync(
        Guid id, IdentifierDto request, ClaimsPrincipal user, AppDbContext db, BudgetService budget, CancellationToken ct)
    {
        var familyId = user.GetFamilyId();
        if (!Enum.IsDefined(request.Kind) || string.IsNullOrWhiteSpace(request.Value) || request.Value.Length > 255)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Value)] = ["A known kind and a value of at most 255 characters are required."],
            });
        }

        var apps = await db.VisibleAppsAsync(familyId, ct);
        if (apps.All(a => a.Id != id))
            return TypedResults.NotFound();

        var value = request.Value.Trim();
        var existing = apps.FirstOrDefault(a => a.Identifiers.Any(i => i.Kind == request.Kind && string.Equals(i.Value, value, StringComparison.OrdinalIgnoreCase)));
        if (existing is not null)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Identifier already in use",
                Detail = $"\"{value}\" already belongs to {existing.Name}.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var identifier = new AppIdentifier { AppDefinitionId = id, FamilyId = familyId, Kind = request.Kind, Value = value };
        db.AppIdentifiers.Add(identifier);
        await db.SaveChangesAsync(ct);

        var affectedChildren = await ReassignUnmappedUsageAsync(db, familyId, ct);
        foreach (var childId in affectedChildren)
            await budget.PushAsync(childId, ct);

        return TypedResults.Created($"/api/apps/{id}/identifiers/{identifier.Id}", IdentifierResponse.From(identifier));
    }

    /// <summary>Removes one of the family's own identifiers. Built-in identifiers can't be removed.</summary>
    private static async Task<Results<NoContent, NotFound>> RemoveIdentifierAsync(
        Guid id, Guid identifierId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var deleted = await db.AppIdentifiers
            .Where(i => i.Id == identifierId && i.AppDefinitionId == id && i.FamilyId == user.GetFamilyId())
            .ExecuteDeleteAsync(ct);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    /// <summary>Identifiers seen on the family's devices in the last 7 days that aren't mapped to an app yet.</summary>
    private static async Task<Ok<List<UnassignedIdentifierResponse>>> UnassignedAsync(
        ClaimsPrincipal user, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var familyId = user.GetFamilyId();
        var now = time.GetUtcNow().UtcDateTime;
        var since = now.AddDays(-7);
        var records = await db.UsageRecords.AsNoTracking()
            .Where(u => u.AppDefinitionId == null && u.StartUtc >= since && u.Device.Child.FamilyId == familyId)
            .Select(u => new { u.Kind, u.RawIdentifier, u.StartUtc, u.Duration })
            .ToListAsync(ct);

        var result = records
            .GroupBy(u => (u.Kind, Raw: u.RawIdentifier.ToLowerInvariant()))
            .Select(g => new UnassignedIdentifierResponse(
                g.Key.Kind,
                g.First().RawIdentifier,
                g.Sum(u => u.Duration.TotalMinutes),
                g.Max(u => u.StartUtc)))
            .OrderByDescending(u => u.LastSeenUtc)
            .ToList();
        return TypedResults.Ok(result);
    }

    /// <summary>Maps the family's unmapped usage with the current catalog. Returns the children whose usage changed.</summary>
    private static async Task<List<Guid>> ReassignUnmappedUsageAsync(AppDbContext db, Guid familyId, CancellationToken ct)
    {
        var resolver = new AppResolver((await db.VisibleAppsAsync(familyId, ct)).ToCoreCatalog());
        var unmapped = await db.UsageRecords
            .Where(u => u.AppDefinitionId == null && u.Device.Child.FamilyId == familyId)
            .ToListAsync(ct);

        var changed = new HashSet<Guid>();
        foreach (var record in unmapped)
        {
            if (resolver.Resolve(record.Kind, record.RawIdentifier) is not { } app)
                continue;
            record.AppDefinitionId = app.Id;
            changed.Add(record.ChildId);
        }
        await db.SaveChangesAsync(ct);
        return [.. changed];
    }

    private static ValidationProblem? NameError(AppRequest request) =>
        string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100
            ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Name)] = ["Name is required and must be at most 100 characters."] })
            : null;
}
