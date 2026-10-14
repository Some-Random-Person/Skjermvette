using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ScreenTime.Core;
using ScreenTime.Core.Models;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;
using ScreenTime.Server.Services;

namespace ScreenTime.Server.Endpoints;

public static class ChildEndpoints
{
    public static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(10);

    public static void MapChildEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/children").WithTags("Children").RequireAuthorization(AppClaims.ParentPolicy);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/pairing-codes", CreatePairingCodeAsync);
        group.MapGet("/{id:guid}/today", TodayAsync);
        group.MapPost("/{id:guid}/extra-time", GrantExtraTimeAsync);
    }

    /// <summary>The family's children with their rules.</summary>
    private static async Task<Ok<List<ChildResponse>>> ListAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var children = await db.Children.AsNoTracking().Include(c => c.AppRules)
            .Where(c => c.FamilyId == user.GetFamilyId())
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return TypedResults.Ok(children.Select(ChildResponse.From).ToList());
    }

    /// <summary>One child with their rules.</summary>
    private static async Task<Results<Ok<ChildResponse>, NotFound>> GetAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var child = await FindAsync(db, user, id, ct);
        return child is null ? TypedResults.NotFound() : TypedResults.Ok(ChildResponse.From(child));
    }

    /// <summary>Adds a child with their daily total, offline allowance and app rules.</summary>
    private static async Task<Results<Created<ChildResponse>, ValidationProblem>> CreateAsync(
        ChildRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var errors = await ValidateAsync(request, user, db, ct);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var child = new Child { FamilyId = user.GetFamilyId(), Name = request.Name, TimeZoneId = request.TimeZoneId };
        Apply(child, request);
        db.Children.Add(child);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/children/{child.Id}", ChildResponse.From(child));
    }

    /// <summary>Replaces a child's settings and rules. Devices get the new budget straight away.</summary>
    private static async Task<Results<Ok<ChildResponse>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, ChildRequest request, ClaimsPrincipal user, AppDbContext db, BudgetService budget, CancellationToken ct)
    {
        var child = await FindAsync(db, user, id, ct);
        if (child is null)
            return TypedResults.NotFound();

        var errors = await ValidateAsync(request, user, db, ct);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        db.AppRules.RemoveRange(child.AppRules);
        child.AppRules.Clear();
        Apply(child, request);
        await db.SaveChangesAsync(ct);

        await budget.PushAsync(child.Id, ct);
        return TypedResults.Ok(ChildResponse.From(child));
    }

    /// <summary>Removes a child together with their devices and usage history.</summary>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var child = await FindAsync(db, user, id, ct);
        if (child is null)
            return TypedResults.NotFound();

        db.Children.Remove(child);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Creates a one-time 6-digit code, valid for 10 minutes, that a device uses to join this child.</summary>
    private static async Task<Results<Ok<PairingCodeResponse>, NotFound>> CreatePairingCodeAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var child = await FindAsync(db, user, id, ct);
        if (child is null)
            return TypedResults.NotFound();

        var now = time.GetUtcNow().UtcDateTime;
        await db.PairingCodes.Where(p => p.ExpiresAtUtc <= now).ExecuteDeleteAsync(ct);

        string code;
        do code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        while (await db.PairingCodes.AnyAsync(p => p.Code == code, ct));

        var pairing = new PairingCode { ChildId = child.Id, Code = code, ExpiresAtUtc = now + PairingCodeLifetime };
        db.PairingCodes.Add(pairing);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new PairingCodeResponse(pairing.Code, pairing.ExpiresAtUtc));
    }

    /// <summary>Today's usage per app and per device, as the parent sees it (without offline allowances set aside).</summary>
    private static async Task<Results<Ok<TodayResponse>, NotFound>> TodayAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, BudgetService budget, TimeProvider time, CancellationToken ct)
    {
        if (await FindAsync(db, user, id, ct) is null)
            return TypedResults.NotFound();

        var day = await budget.LoadDayAsync(id, ct);
        var snapshot = BudgetCalculator.Calculate(day.Policy, day.Day, day.Events, []);
        var apps = (await db.VisibleAppsAsync(user.GetFamilyId(), ct)).ToDictionary(a => a.Id, a => a.Name);
        var rules = day.Child.AppRules.ToDictionary(r => r.AppDefinitionId);

        var usedByApp = day.Events
            .GroupBy(e => e.LogicalAppId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => BudgetCalculator.UsedTime(g, day.Day));

        var appRows = rules.Keys.Union(usedByApp.Keys)
            .Select(appId =>
            {
                var rule = rules.GetValueOrDefault(appId);
                var used = usedByApp.GetValueOrDefault(appId).TotalMinutes;
                return appId == Guid.Empty
                    ? new TodayAppResponse(null, "Other apps", null, used, null, snapshot.TotalRemaining.TotalMinutes)
                    : new TodayAppResponse(appId, apps.GetValueOrDefault(appId, "Unknown app"), rule?.Kind, used,
                        rule?.Limit?.TotalMinutes, snapshot.Remaining(appId)?.TotalMinutes);
            })
            .OrderByDescending(a => a.UsedMinutes)
            .ToList();

        var now = time.GetUtcNow().UtcDateTime;
        return TypedResults.Ok(new TodayResponse(
            id,
            day.Day.Start,
            day.Day.End,
            day.Policy.DailyTotal.TotalMinutes,
            day.Extra.TotalMinutes,
            snapshot.TotalUsed.TotalMinutes,
            snapshot.TotalRemaining.TotalMinutes,
            appRows,
            day.Child.Devices.OrderBy(d => d.Name).Select(d => DeviceResponse.From(d, now)).ToList()));
    }

    /// <summary>Adds extra minutes to today's daily total only.</summary>
    private static async Task<Results<NoContent, NotFound, ValidationProblem>> GrantExtraTimeAsync(
        Guid id, ExtraTimeRequest request, ClaimsPrincipal user, AppDbContext db, BudgetService budget, TimeProvider time, CancellationToken ct)
    {
        if (request.Minutes is < 1 or > 24 * 60)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Minutes)] = ["Extra time must be between 1 and 1440 minutes."] });

        var child = await FindAsync(db, user, id, ct);
        if (child is null)
            return TypedResults.NotFound();

        var now = time.GetUtcNow();
        db.ExtraTimeGrants.Add(new ExtraTimeGrant
        {
            ChildId = id,
            DayStartUtc = TimeWindow.Day(now, child.TimeZoneId, child.ResetHour).Start.UtcDateTime,
            Amount = TimeSpan.FromMinutes(request.Minutes),
            GrantedAtUtc = now.UtcDateTime,
        });
        await db.SaveChangesAsync(ct);

        await budget.PushAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static Task<Child?> FindAsync(AppDbContext db, ClaimsPrincipal user, Guid id, CancellationToken ct) =>
        db.Children.Include(c => c.AppRules).FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == user.GetFamilyId(), ct);

    private static async Task<Dictionary<string, string[]>> ValidateAsync(ChildRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var familyId = user.GetFamilyId();
        var visibleAppIds = await db.AppDefinitions
            .Where(a => a.FamilyId == null || a.FamilyId == familyId)
            .Select(a => a.Id)
            .ToListAsync(ct);
        return ChildValidation.Validate(request, visibleAppIds.ToHashSet());
    }

    private static void Apply(Child child, ChildRequest request)
    {
        child.Name = request.Name.Trim();
        child.TimeZoneId = request.TimeZoneId;
        child.ResetHour = request.ResetHour;
        child.DailyTotal = TimeSpan.FromMinutes(request.DailyTotalMinutes);
        child.OfflineAllowance = TimeSpan.FromMinutes(request.OfflineAllowanceMinutes);
        child.AppRules.AddRange(request.Rules.Select(r => new AppRule
        {
            AppDefinitionId = r.AppId,
            Kind = r.Kind,
            Limit = r.Kind == AppRuleKindEnum.Limited ? TimeSpan.FromMinutes(r.LimitMinutes!.Value) : null,
        }));
    }
}
