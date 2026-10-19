using Skjermvette.Agent.Service;
using Skjermvette.Server.Tests;

namespace Skjermvette.Agent.Tests;

public class HelperSupervisorTests
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly HelperSupervisor _supervisor;

    public HelperSupervisorTests() => _supervisor = new HelperSupervisor(_clock);

    [Fact]
    public void NobodySignedIn_NothingIsStarted()
    {
        Assert.Equal(HelperActionEnum.None, _supervisor.Next(null, null));
    }

    [Fact]
    public void AUserSignsIn_TheHelperIsStarted()
    {
        Assert.Equal(HelperActionEnum.Start, _supervisor.Next(1, null));
        Assert.Equal(HelperActionEnum.None, _supervisor.Next(1, 1));
    }

    [Fact]
    public void WhileTheSessionIsSwitching_TheHelperKeepsRunning()
    {
        _supervisor.Next(1, null);

        Assert.Equal(HelperActionEnum.None, _supervisor.Next(null, 1));
    }

    [Fact]
    public void AnotherUserSignsIn_TheOldHelperIsClosedAndANewOneStartedStraightAway()
    {
        _supervisor.Next(1, null);

        Assert.Equal(HelperActionEnum.Stop, _supervisor.Next(2, 1));
        Assert.Equal(HelperActionEnum.Start, _supervisor.Next(2, null));
    }

    [Fact]
    public void AHelperKilledAfterRunningAWhile_IsStartedAgainStraightAway()
    {
        _supervisor.Next(1, null);
        _clock.Advance(HelperSupervisor.RanLong);

        Assert.Equal(HelperActionEnum.Start, _supervisor.Next(1, null));
    }

    [Fact]
    public void AHelperThatKeepsStopping_IsStartedLessAndLessOften()
    {
        _supervisor.Next(1, null);

        foreach (var delay in new[] { 1, 2, 4, 8, 16, 30, 30 })
        {
            Assert.Equal(HelperActionEnum.None, _supervisor.Next(1, null));
            Assert.Equal(TimeSpan.FromSeconds(delay), _supervisor.Delay);
            _clock.Advance(TimeSpan.FromSeconds(delay) - TimeSpan.FromMilliseconds(1));
            Assert.Equal(HelperActionEnum.None, _supervisor.Next(1, null));
            _clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(HelperActionEnum.Start, _supervisor.Next(1, null));
        }
    }

    [Fact]
    public void AfterRunningAWhile_TheDelayStartsOver()
    {
        _supervisor.Next(1, null);
        _supervisor.Next(1, null);
        _clock.Advance(HelperSupervisor.FirstDelay);
        _supervisor.Next(1, null);
        _clock.Advance(HelperSupervisor.RanLong);

        Assert.Equal(HelperActionEnum.Start, _supervisor.Next(1, null));
        Assert.Equal(TimeSpan.Zero, _supervisor.Delay);
        Assert.Equal(HelperActionEnum.None, _supervisor.Next(1, null));
        Assert.Equal(HelperSupervisor.FirstDelay, _supervisor.Delay);
    }

    [Fact]
    public void AFailedStart_IsTriedAgainLater()
    {
        _supervisor.Next(1, null);
        _supervisor.Failed();

        Assert.Equal(HelperActionEnum.None, _supervisor.Next(1, null));
        _clock.Advance(HelperSupervisor.FirstDelay);
        Assert.Equal(HelperActionEnum.Start, _supervisor.Next(1, null));
        _supervisor.Failed();
        Assert.Equal(TimeSpan.FromSeconds(2), _supervisor.Delay);
    }
}
