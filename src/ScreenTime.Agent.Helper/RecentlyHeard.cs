namespace ScreenTime.Agent.Helper;

/// <summary>
/// Keeps a program counted as making sound for a few seconds after it was last heard, so short pauses (between words,
/// or between songs) don't cut its time into pieces or let the computer go idle.
/// </summary>
public sealed class RecentlyHeard
{
    public static readonly TimeSpan HoldFor = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, TimeSpan> _lastHeard = [];

    /// <param name="now">Any steadily increasing clock, e.g. time since boot.</param>
    /// <param name="heard">The programs making sound right now.</param>
    /// <returns>The programs heard within the last <see cref="HoldFor"/>.</returns>
    public IReadOnlyCollection<string> Update(TimeSpan now, IEnumerable<string> heard)
    {
        foreach (var program in heard)
            _lastHeard[program] = now;
        foreach (var (program, last) in _lastHeard.ToList())
        {
            if (now - last > HoldFor)
                _lastHeard.Remove(program);
        }
        return _lastHeard.Keys;
    }
}
