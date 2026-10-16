using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using ScreenTime.Core;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Service;

/// <summary>
/// One check-in with the server (<see cref="TickAsync"/>, about every 15 s): save new usage to the outbox, upload it,
/// renew the offline allowance and keep the budget hub connected. When the server can't be reached, the budget is
/// worked out locally from the offline allowance.
/// </summary>
public sealed class DeviceSync : IAsyncDisposable
{
    /// <summary>The server's limit per upload.</summary>
    public const int MaxEventsPerBatch = 500;

    /// <summary>App mappings aren't pushed, so they're read again this often.</summary>
    public static readonly TimeSpan CatalogRefresh = TimeSpan.FromMinutes(5);

    private readonly DeviceIdentity _identity;
    private readonly string _cachePath;
    private readonly UsageTracker _tracker;
    private readonly BudgetState _state;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly ServerClient _client;
    private readonly HubConnection _hub;
    private readonly Outbox _outbox;
    private CachedBudget? _cache;
    private DateTimeOffset? _catalogLoadedAt;

    public DeviceSync(DeviceIdentity identity, string folder, UsageTracker tracker, BudgetState state, TimeProvider time, ILogger logger,
        Func<HttpMessageHandler>? createHandler = null)
    {
        _identity = identity;
        _cachePath = Path.Combine(folder, "budget.json");
        _tracker = tracker;
        _state = state;
        _time = time;
        _logger = logger;
        _client = new ServerClient(identity, createHandler);
        _hub = _client.CreateHub(snapshot => _state.Set(ConnectionEnum.Online, snapshot));
        _outbox = new Outbox(folder);
        _cache = File.Exists(_cachePath) ? JsonSerializer.Deserialize<CachedBudget>(File.ReadAllText(_cachePath), ServerClient.Json) : null;
        if (_cache is not null)
            _state.SetCatalog(_cache.Catalog);
    }

    /// <exception cref="UnpairedException">The device has been unpaired; stop.</exception>
    public async Task TickAsync(CancellationToken ct)
    {
        var now = await SaveUsageAsync(ct);
        try
        {
            if (_cache is null || _catalogLoadedAt is null || now - _catalogLoadedAt >= CatalogRefresh)
            {
                var state = await _client.StateAsync(ct);
                _cache = new CachedBudget(state.Catalog, state.Snapshot.ExemptAppIds, _cache?.Lease);
                _catalogLoadedAt = now;
                _state.SetCatalog(state.Catalog);
            }

            var sync = await UploadAsync(ct);
            // Offline use is counted from the outbox, which now holds only what was used after `now`, so the allowance
            // starts then by this device's clock, whatever the server's clock says.
            _cache = _cache with { ExemptAppIds = sync.Snapshot.ExemptAppIds, Lease = sync.Lease with { GrantedAt = now } };
            SaveCache(_cache);
            _state.Set(ConnectionEnum.Online, sync.Snapshot);
            await ConnectHubAsync(ct);
        }
        catch (HttpRequestException e)
        {
            if (_state.Connection != ConnectionEnum.Offline)
                _logger.LogWarning("Can't reach the server ({Message}). Using the offline allowance.", e.Message);
            _state.Set(ConnectionEnum.Offline, await OfflineSnapshotAsync(now, ct));
        }
        catch (UnpairedException)
        {
            _state.Set(ConnectionEnum.Unpaired, null);
            throw;
        }
    }

    /// <summary>Moves usage recorded so far into the outbox, e.g. before stopping. Returns the time it was cut at.</summary>
    public async Task<DateTimeOffset> SaveUsageAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        await _outbox.AddAsync(_tracker.Flush(now), ct);
        return now;
    }

    public async ValueTask DisposeAsync()
    {
        await _hub.DisposeAsync();
        _client.Dispose();
    }

    /// <summary>Uploads the outbox in batches (an empty one when there's nothing to send) and returns the last reply.</summary>
    private async Task<SyncResponse> UploadAsync(CancellationToken ct)
    {
        while (true)
        {
            var batch = await _outbox.ListAsync(MaxEventsPerBatch, ct);
            var ids = batch.Select(e => e.Id).ToList();
            SyncResponse sync;
            try
            {
                sync = await _client.SyncAsync(batch, ct);
            }
            catch (ServerRejectedException e) when (batch.Count > 0)
            {
                // Sending them again would fail the same way and hold up everything recorded after them.
                _logger.LogError("The server refused {Count} usage events, so they were dropped. {Problem}", batch.Count, e.Message);
                await _outbox.RemoveAsync(ids, ct);
                continue;
            }

            await _outbox.RemoveAsync(ids, ct);
            if (batch.Count < MaxEventsPerBatch)
                return sync;
        }
    }

    private async Task ConnectHubAsync(CancellationToken ct)
    {
        if (_hub.State != HubConnectionState.Disconnected)
            return;
        try
        {
            await _hub.StartAsync(ct);
        }
        catch (HttpRequestException e)
        {
            // Not essential: the next check-in brings the budget too. Tried again then.
            _logger.LogWarning("Can't connect for live updates ({Message}).", e.Message);
        }
    }

    /// <summary>
    /// The budget from the offline allowance, less what was used since it was granted. Without an allowance (none
    /// yet, used up, or expired at the end of the day) everything that isn't exempt is blocked.
    /// </summary>
    private async Task<BudgetSnapshot> OfflineSnapshotAsync(DateTimeOffset now, CancellationToken ct)
    {
        var exempt = _cache?.ExemptAppIds ?? [];
        if (_cache?.Lease is not { } lease || !lease.IsActive(now))
            return new BudgetSnapshot(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, [], exempt);

        var resolver = new AppResolver(_cache.Catalog);
        var usage = (await _outbox.ListAsync(int.MaxValue, ct)).Select(e => new UsageEvent(
            e.Id, _identity.DeviceId, resolver.Resolve(e.Kind, e.RawIdentifier)?.Id, e.RawIdentifier, e.Start, TimeSpan.FromSeconds(e.DurationSeconds)));
        return BudgetCalculator.CalculateOffline(lease, exempt, usage);
    }

    private void SaveCache(CachedBudget cache)
    {
        // Write then replace, so a crash halfway never leaves a broken file.
        var temp = _cachePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(cache, ServerClient.Json));
        File.Move(temp, _cachePath, overwrite: true);
    }
}
