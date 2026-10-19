using Skjermvette.Core.Models;
using static Skjermvette.Core.Tests.Fixtures;

namespace Skjermvette.Core.Tests;

public class BudgetCalculatorTests
{
    private static BudgetSnapshot Calculate(IEnumerable<UsageEvent> events, IEnumerable<Lease>? leases = null) =>
        BudgetCalculator.Calculate(Policy(), Day, events, leases ?? []);

    [Fact]
    public void YouTubeAtLimit_BlocksYouTube_AndLeavesRestOfTotal()
    {
        var snapshot = Calculate([Use(YouTube, 30)]);

        Assert.True(snapshot.IsBlocked(YouTube));
        Assert.Equal(Min(90), snapshot.TotalRemaining);
        Assert.Equal(Min(90), snapshot.Remaining(Minecraft));
        Assert.False(snapshot.IsBlocked(Minecraft));
    }

    [Fact]
    public void ExemptApp_DoesNotReduceTotal_AndIsNeverBlocked()
    {
        var snapshot = Calculate([Use(Teams, 30), Use(Minecraft, 120)]);

        Assert.Equal(TimeSpan.Zero, snapshot.TotalRemaining);
        Assert.Null(snapshot.Remaining(Teams));
        Assert.False(snapshot.IsBlocked(Teams));
        Assert.True(snapshot.IsBlocked(Minecraft));
    }

    [Fact]
    public void UsageOnAnotherDevice_CountsTowardSameLimits()
    {
        var snapshot = Calculate([Use(YouTube, 15, device: Phone)]);

        Assert.Equal(Min(15), snapshot.Remaining(YouTube));
        Assert.Equal(Min(105), snapshot.TotalRemaining);
    }

    [Fact]
    public void SameAppOnTwoDevicesAtOnce_CountsOnce_ForTheTotalAndTheApp()
    {
        var snapshot = Calculate([Use(YouTube, 10, device: Phone), Use(YouTube, 10, device: Pc)]);

        Assert.Equal(Min(10), snapshot.TotalUsed);
        Assert.Equal(Min(20), snapshot.Remaining(YouTube));
    }

    [Fact]
    public void DifferentAppsOnTwoDevicesAtOnce_CountOnceForTheTotal_AndFullyForEachApp()
    {
        var snapshot = Calculate([Use(YouTube, 10, device: Phone), Use(Minecraft, 10, device: Pc)]);

        Assert.Equal(Min(10), snapshot.TotalUsed);
        Assert.Equal(Min(20), snapshot.Remaining(YouTube));
    }

    [Fact]
    public void BackgroundAudio_CountsTowardTheAppsLimit_ButNotTwiceTowardTheTotal()
    {
        // YouTube plays in a background tab while another site is in front, on the same PC.
        var snapshot = Calculate([Use(YouTube, 10), Use(null, 10)]);

        Assert.Equal(Min(10), snapshot.TotalUsed);
        Assert.Equal(Min(20), snapshot.Remaining(YouTube));
    }

    [Fact]
    public void PartlyOverlappingUse_CountsTheCoveredTimeOnce()
    {
        // Phone 10:00–10:10, PC 10:05–10:20: 20 minutes on the clock.
        var phone = Use(Minecraft, 10, device: Phone);
        var pc = Use(Minecraft, 15, device: Pc) with { Start = phone.Start.AddMinutes(5) };

        Assert.Equal(Min(20), Calculate([phone, pc]).TotalUsed);
    }

    [Fact]
    public void UseAtDifferentTimes_IsAddedUp()
    {
        var snapshot = Calculate([Use(Minecraft, 10, device: Phone, startHour: 9), Use(Minecraft, 10, device: Pc, startHour: 11)]);

        Assert.Equal(Min(20), snapshot.TotalUsed);
    }

    [Fact]
    public void ExemptUseAtTheSameTime_DoesNotHideCountedUse()
    {
        var snapshot = Calculate([Use(Teams, 30, device: Pc), Use(Minecraft, 10, device: Phone)]);

        Assert.Equal(Min(10), snapshot.TotalUsed);
    }

    [Fact]
    public void LimitedApp_IsCappedByTotalRemaining()
    {
        var snapshot = Calculate([Use(Minecraft, 110)]);

        Assert.Equal(Min(10), snapshot.Remaining(YouTube));
    }

    [Fact]
    public void UnmappedApp_CountsTowardTotal()
    {
        var snapshot = Calculate([Use(null, 20)]);

        Assert.Equal(Min(100), snapshot.Remaining(null));
    }

    [Fact]
    public void EventsOutsideTheDay_AreClipped()
    {
        var crossesMidnight = new UsageEvent(Guid.NewGuid(), Pc, Minecraft, "raw", Day.Start.AddMinutes(-10), Min(25));

        Assert.Equal(Min(15), Calculate([crossesMidnight]).TotalUsed);
    }

    [Fact]
    public void AppBothLimitedAndExempt_IsTreatedAsExempt()
    {
        var policy = Policy() with { ExemptAppIds = new HashSet<Guid> { Teams, YouTube } };

        var snapshot = BudgetCalculator.Calculate(policy, Day, [Use(YouTube, 60)], []);

        Assert.Null(snapshot.Remaining(YouTube));
        Assert.Equal(Min(120), snapshot.TotalRemaining);
    }
}
