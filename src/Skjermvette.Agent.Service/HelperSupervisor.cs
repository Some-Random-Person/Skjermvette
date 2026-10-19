using System.ComponentModel;

namespace Skjermvette.Agent.Service;

/// <summary>
/// Decides when to start the Helper in the signed-in user's session, and when to close it because another user is now
/// signed in at the computer. A Helper that stops after running a while (e.g. killed by the child) is started again
/// straight away; one that keeps stopping soon after it starts, or can't be started, is retried less and less often.
/// </summary>
public sealed class HelperSupervisor(TimeProvider time)
{
    /// <summary>A Helper that ran at least this long is started again straight away when it stops.</summary>
    public static readonly TimeSpan RanLong = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private DateTimeOffset? _startedAt;
    private DateTimeOffset _startAfter = DateTimeOffset.MinValue;

    /// <summary>How long the last stop or failed start made it wait before starting the Helper again.</summary>
    public TimeSpan Delay { get; private set; } = TimeSpan.Zero;

    /// <summary>What to do now.</summary>
    /// <param name="userSession">The console session, when a user is signed in to it.</param>
    /// <param name="helperSession">The session the Helper runs in, or null when it isn't running.</param>
    public HelperActionEnum Next(int? userSession, int? helperSession)
    {
        var now = time.GetUtcNow();
        if (helperSession is not null)
        {
            if (userSession is null || userSession == helperSession)
                return HelperActionEnum.None;
            // Another user is signed in: their own Helper starts straight away.
            _startedAt = null;
            _startAfter = now;
            return HelperActionEnum.Stop;
        }

        if (_startedAt is { } startedAt)
        {
            _startedAt = null;
            WaitAfterStop(now, ranLong: now - startedAt >= RanLong);
        }
        if (userSession is null || now < _startAfter)
            return HelperActionEnum.None;

        _startedAt = now;
        return HelperActionEnum.Start;
    }

    /// <summary>The Helper couldn't be started.</summary>
    public void Failed()
    {
        _startedAt = null;
        WaitAfterStop(time.GetUtcNow(), ranLong: false);
    }

    private void WaitAfterStop(DateTimeOffset now, bool ranLong)
    {
        Delay = ranLong ? TimeSpan.Zero
            : Delay == TimeSpan.Zero ? FirstDelay
            : TimeSpan.FromTicks(Math.Min(Delay.Ticks * 2, MaxDelay.Ticks));
        _startAfter = now + Delay;
    }
}

public enum HelperActionEnum
{
    [Description("Nothing to do")]
    None = 1,

    [Description("Start the Helper in the user's session")]
    Start = 2,

    [Description("Close the Helper, which runs in another user's session")]
    Stop = 3,
}
