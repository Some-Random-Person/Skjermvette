using Microsoft.EntityFrameworkCore;
using Skjermvette.Core.DeviceApi;

namespace Skjermvette.Agent.Service;

/// <summary>
/// Usage events waiting to be uploaded, in <c>outbox.db</c> (SQLite), so nothing is lost while offline or across
/// restarts. Events are removed once the server has them, so what's left is also the usage since the last sync.
/// </summary>
public sealed class Outbox
{
    private readonly DbContextOptions<OutboxDbContext> _options;

    public Outbox(string folder)
    {
        // No pooling: nothing stays open between ticks, and the file can be deleted (tests, 'forget').
        _options = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseSqlite($"Data Source={Path.Combine(folder, "outbox.db")};Pooling=False")
            .Options;
        using var db = new OutboxDbContext(_options);
        db.Database.EnsureCreated();
    }

    public async Task AddAsync(IReadOnlyCollection<UsageEventDto> events, CancellationToken ct)
    {
        if (events.Count == 0)
            return;
        await using var db = new OutboxDbContext(_options);
        db.Events.AddRange(events.Select(e => new OutboxEvent
        {
            Id = e.Id,
            Kind = e.Kind,
            RawIdentifier = e.RawIdentifier,
            StartUtc = e.Start.UtcDateTime,
            DurationSeconds = e.DurationSeconds,
        }));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The oldest <paramref name="max"/> events, or all of them.</summary>
    public async Task<List<UsageEventDto>> ListAsync(int max, CancellationToken ct)
    {
        await using var db = new OutboxDbContext(_options);
        var events = await db.Events.AsNoTracking().OrderBy(e => e.StartUtc).Take(max).ToListAsync(ct);
        return events.Select(e => new UsageEventDto(e.Id, e.Kind, e.RawIdentifier, new DateTimeOffset(e.StartUtc, TimeSpan.Zero), e.DurationSeconds)).ToList();
    }

    public async Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        await using var db = new OutboxDbContext(_options);
        await db.Events.Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(ct);
    }
}
