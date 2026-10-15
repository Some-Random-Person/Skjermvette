using ScreenTime.Core.DeviceApi;

namespace ScreenTime.Agent.Service;

/// <summary>
/// Turns samples of what is in use (about every second: the foreground app or tab, plus anything playing audio)
/// into usage events. Each key gets its own events; the server merges overlaps so a minute counts once.
/// Thread-safe: samples come from the Helper while the sync loop flushes.
/// </summary>
public sealed class UsageTracker
{
    /// <summary>A key missing from samples for longer than this has stopped (e.g. the Helper was restarted).</summary>
    public static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(5);

    private readonly Lock _lock = new();
    private readonly Dictionary<UsageKey, Stretch> _open = [];
    private readonly List<UsageEventDto> _done = [];

    /// <summary>Records that <paramref name="inUse"/> are in use at <paramref name="now"/>, and nothing else is.</summary>
    public void Observe(DateTimeOffset now, IReadOnlyCollection<UsageKey> inUse)
    {
        lock (_lock)
        {
            foreach (var (key, stretch) in _open.ToList())
            {
                if (!inUse.Contains(key) || now - stretch.LastSeen > MaxGap)
                    Close(key, stretch);
            }

            foreach (var key in inUse)
            {
                if (_open.TryGetValue(key, out var stretch))
                    stretch.LastSeen = now;
                else
                    _open[key] = new Stretch(now);
            }
        }
    }

    /// <summary>Everything used up to <paramref name="now"/> that hasn't been returned yet. Ongoing use carries on in new events.</summary>
    public List<UsageEventDto> Flush(DateTimeOffset now)
    {
        lock (_lock)
        {
            foreach (var (key, stretch) in _open.ToList())
            {
                if (now - stretch.LastSeen > MaxGap)
                {
                    Close(key, stretch);
                }
                else
                {
                    Add(key, stretch.Start, stretch.LastSeen);
                    stretch.Start = stretch.LastSeen;
                }
            }

            var events = _done.ToList();
            _done.Clear();
            return events;
        }
    }

    private void Close(UsageKey key, Stretch stretch)
    {
        Add(key, stretch.Start, stretch.LastSeen);
        _open.Remove(key);
    }

    private void Add(UsageKey key, DateTimeOffset start, DateTimeOffset end)
    {
        if (end > start)
            _done.Add(new UsageEventDto(Guid.NewGuid(), key.Kind, key.Identifier, start, (end - start).TotalSeconds));
    }

    private sealed class Stretch(DateTimeOffset start)
    {
        public DateTimeOffset Start { get; set; } = start;
        public DateTimeOffset LastSeen { get; set; } = start;
    }
}
