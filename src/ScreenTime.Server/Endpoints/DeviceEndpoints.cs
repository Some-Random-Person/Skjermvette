using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;
using ScreenTime.Server.Services;

namespace ScreenTime.Server.Endpoints;

public static class DeviceEndpoints
{
    public const string PairingRateLimit = "pairing";
    public const int MaxEventsPerBatch = 500;

    /// <summary>How far in the future an event may start, to tolerate device clocks that run a little fast.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var parent = app.MapGroup("/api/devices").WithTags("Devices").RequireAuthorization(AppClaims.ParentPolicy);
        parent.MapGet("/", ListAsync);
        parent.MapPut("/{id:guid}", UpdateAsync);
        parent.MapDelete("/{id:guid}", UnpairAsync);

        app.MapPost("/api/device/pair", PairAsync).WithTags("Device API").RequireRateLimiting(PairingRateLimit);

        var device = app.MapGroup("/api/device").WithTags("Device API").RequireAuthorization(AppClaims.DevicePolicy);
        device.MapGet("/state", StateAsync);
        device.MapPost("/usage", UsageAsync);
        device.MapPost("/lease", LeaseAsync);
    }

    /// <summary>The family's devices, optionally for one child.</summary>
    private static async Task<Ok<List<DeviceResponse>>> ListAsync(
        Guid? childId, ClaimsPrincipal user, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var devices = await db.Devices.AsNoTracking().Include(d => d.Lease)
            .Where(d => d.Child.FamilyId == user.GetFamilyId() && d.UnpairedAtUtc == null && (childId == null || d.ChildId == childId))
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        return TypedResults.Ok(devices.Select(d => DeviceResponse.From(d, now)).ToList());
    }

    /// <summary>
    /// Renames a device and sets or clears its own offline allowance. The new allowance applies the
    /// next time the device syncs, which is normally within 15 seconds.
    /// </summary>
    private static async Task<Results<NoContent, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, DeviceUpdateRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var maxAllowance = (int)Policy.MaxOfflineAllowance.TotalMinutes;
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100
            || request.OfflineAllowanceOverrideMinutes is < 0 || request.OfflineAllowanceOverrideMinutes > maxAllowance)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Device"] = [$"Name is required (at most 100 characters) and the offline allowance must be between 0 and {maxAllowance} minutes."],
            });
        }

        var device = await FindPairedAsync(db, user, id, ct);
        if (device is null)
            return TypedResults.NotFound();

        device.Name = request.Name.Trim();
        device.OfflineAllowanceOverride = request.OfflineAllowanceOverrideMinutes is { } m ? TimeSpan.FromMinutes(m) : null;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Unpairs a device: its token stops working and it disappears from the dashboard.
    /// Its usage is kept, so time it used today still counts.
    /// </summary>
    private static async Task<Results<NoContent, NotFound>> UnpairAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, BudgetService budget, TimeProvider time, CancellationToken ct)
    {
        var device = await FindPairedAsync(db, user, id, ct);
        if (device is null)
            return TypedResults.NotFound();

        // Keep the row for its usage, but give it a token nobody knows and release its offline allowance.
        device.UnpairedAtUtc = time.GetUtcNow().UtcDateTime;
        device.TokenHash = DeviceTokenHandler.Hash(DeviceTokenHandler.NewToken());
        await db.DeviceLeases.Where(l => l.DeviceId == id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);

        await budget.PushAsync(device.ChildId, ct);
        return TypedResults.NoContent();
    }

    /// <summary>Exchanges a pairing code for a device token. The code works once and expires after 10 minutes.</summary>
    private static async Task<Results<Ok<PairResponse>, ProblemHttpResult, ValidationProblem>> PairAsync(
        PairRequest request, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName) || request.DeviceName.Length > 100 || !Enum.IsDefined(request.Platform))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Device"] = ["A device name (at most 100 characters) and a known platform are required."],
            });
        }

        var now = time.GetUtcNow().UtcDateTime;
        var code = (request.Code ?? "").Trim();
        var pairing = await db.PairingCodes.FirstOrDefaultAsync(p => p.Code == code && p.ExpiresAtUtc > now, ct);
        if (pairing is null)
            return TypedResults.Problem("This code is wrong or has expired. Create a new one in the dashboard.", statusCode: StatusCodes.Status400BadRequest);

        var token = DeviceTokenHandler.NewToken();
        var device = new Device
        {
            ChildId = pairing.ChildId,
            Name = request.DeviceName.Trim(),
            Platform = request.Platform,
            TokenHash = DeviceTokenHandler.Hash(token),
            PairedAtUtc = now,
        };
        db.Devices.Add(device);
        db.PairingCodes.Remove(pairing);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new PairResponse(device.Id, device.ChildId, token));
    }

    /// <summary>Rules, app catalog, current budget and offline allowance for the calling device.</summary>
    private static async Task<Ok<DeviceStateResponse>> StateAsync(
        ClaimsPrincipal user, AppDbContext db, BudgetService budget, CancellationToken ct)
    {
        var deviceId = user.GetDeviceId();
        var day = await budget.LoadDayAsync(user.GetChildId(), ct);
        var catalog = await db.VisibleAppsAsync(user.GetFamilyId(), ct);
        var lease = day.ActiveLeases.FirstOrDefault(l => l.DeviceId == deviceId);

        return TypedResults.Ok(new DeviceStateResponse(
            deviceId,
            day.Child.Id,
            day.Policy,
            catalog.ToCoreCatalog(),
            day.SnapshotFor(deviceId),
            lease));
    }

    /// <summary>
    /// Uploads usage (safe to re-send: events are matched by id) and renews the device's offline allowance.
    /// Devices should call this about every 15 seconds while in use, and right after reconnecting.
    /// </summary>
    private static async Task<Results<Ok<SyncResponse>, ValidationProblem>> UsageAsync(
        UsageBatchRequest request, ClaimsPrincipal user, BudgetService budget, TimeProvider time, CancellationToken ct)
    {
        var errors = ValidateEvents(request.Events ?? [], time.GetUtcNow());
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        return TypedResults.Ok(await budget.SyncAsync(user.GetDeviceId(), user.GetChildId(), user.GetFamilyId(), request.Events ?? [], ct));
    }

    /// <summary>Renews the device's offline allowance without uploading usage.</summary>
    private static async Task<Ok<SyncResponse>> LeaseAsync(ClaimsPrincipal user, BudgetService budget, CancellationToken ct) =>
        TypedResults.Ok(await budget.SyncAsync(user.GetDeviceId(), user.GetChildId(), user.GetFamilyId(), [], ct));

    /// <summary>A device of the signed-in parent's family that hasn't been unpaired.</summary>
    private static Task<Device?> FindPairedAsync(AppDbContext db, ClaimsPrincipal user, Guid id, CancellationToken ct) =>
        db.Devices.FirstOrDefaultAsync(d => d.Id == id && d.UnpairedAtUtc == null && d.Child.FamilyId == user.GetFamilyId(), ct);

    private static Dictionary<string, string[]> ValidateEvents(IReadOnlyList<UsageEventDto> events, DateTimeOffset now)
    {
        var problems = new List<string>();
        if (events.Count > MaxEventsPerBatch)
            problems.Add($"At most {MaxEventsPerBatch} events per batch.");

        foreach (var e in events)
        {
            if (e.Id == Guid.Empty)
                problems.Add("Every event needs an id.");
            else if (!Enum.IsDefined(e.Kind) || string.IsNullOrWhiteSpace(e.RawIdentifier) || e.RawIdentifier.Length > 255)
                problems.Add($"Event {e.Id}: a known kind and an identifier of at most 255 characters are required.");
            else if (e.DurationSeconds <= 0 || e.DurationSeconds > BudgetService.MaxEventDuration.TotalSeconds)
                problems.Add($"Event {e.Id}: duration must be more than 0 and at most {BudgetService.MaxEventDuration.TotalSeconds} seconds.");
            else if (e.Start > now + MaxClockSkew)
                problems.Add($"Event {e.Id}: starts in the future. Check the device clock.");
        }

        return problems.Count == 0 ? [] : new Dictionary<string, string[]> { [nameof(UsageBatchRequest.Events)] = [.. problems] };
    }
}
