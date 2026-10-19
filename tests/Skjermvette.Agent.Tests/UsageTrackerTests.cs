using Skjermvette.Agent.Service;
using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Tests;

public class UsageTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly UsageKey Chrome = new(MatcherKindEnum.Process, "chrome.exe");
    private static readonly UsageKey YouTube = new(MatcherKindEnum.Domain, "youtube.com");

    [Fact]
    public void SteadyUse_BecomesOneEventPerFlush_AndCarriesOn()
    {
        var tracker = new UsageTracker();
        Sample(tracker, 0, 10, Chrome);

        var first = Assert.Single(tracker.Flush(T0.AddSeconds(10)));
        Sample(tracker, 11, 20, Chrome);
        var second = Assert.Single(tracker.Flush(T0.AddSeconds(20)));

        Assert.Equal((T0, 10), (first.Start, first.DurationSeconds));
        Assert.Equal((T0.AddSeconds(10), 10), (second.Start, second.DurationSeconds));
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void ForegroundAndAudio_AreSeparateEvents_ForTheSameTime()
    {
        var tracker = new UsageTracker();
        Sample(tracker, 0, 15, Chrome, YouTube);

        var events = tracker.Flush(T0.AddSeconds(15));

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal((T0, 15), (e.Start, e.DurationSeconds)));
    }

    [Fact]
    public void AppNoLongerInUse_EndsAtItsLastSample()
    {
        var tracker = new UsageTracker();
        Sample(tracker, 0, 5, Chrome);
        Sample(tracker, 6, 15, YouTube);

        var events = tracker.Flush(T0.AddSeconds(15));

        Assert.Equal((T0, 5), events.Where(e => e.RawIdentifier == "chrome.exe").Select(e => (e.Start, e.DurationSeconds)).Single());
        Assert.Equal((T0.AddSeconds(6), 9), events.Where(e => e.RawIdentifier == "youtube.com").Select(e => (e.Start, e.DurationSeconds)).Single());
    }

    [Fact]
    public void GapInSamples_IsNotCounted()
    {
        var tracker = new UsageTracker();
        Sample(tracker, 0, 5, Chrome);
        // The Helper stops sending for a minute (e.g. it was restarted).
        Sample(tracker, 65, 70, Chrome);

        var events = tracker.Flush(T0.AddSeconds(70));

        Assert.Equal(10, events.Sum(e => e.DurationSeconds));
    }

    [Fact]
    public void NoSamplesAtAll_CountNothing_AfterTheGap()
    {
        var tracker = new UsageTracker();
        Sample(tracker, 0, 5, Chrome);

        Assert.Equal(5, tracker.Flush(T0.AddSeconds(60)).Sum(e => e.DurationSeconds));
        Assert.Empty(tracker.Flush(T0.AddSeconds(75)));
    }

    /// <summary>One sample a second from <paramref name="from"/> to <paramref name="to"/> seconds after T0, inclusive.</summary>
    private static void Sample(UsageTracker tracker, int from, int to, params UsageKey[] inUse)
    {
        for (var s = from; s <= to; s++)
            tracker.Observe(T0.AddSeconds(s), inUse);
    }
}
