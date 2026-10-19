using Skjermvette.Core.Models;
using static Skjermvette.Core.Tests.Fixtures;

namespace Skjermvette.Core.Tests;

public class OfflineAllowanceTests
{
    private static readonly DateTimeOffset Now = Day.Start.AddHours(9);

    private static Lease GrantFor(Guid device, TimeSpan allowance, IEnumerable<UsageEvent>? events = null, IEnumerable<Lease>? others = null)
    {
        var snapshot = BudgetCalculator.Calculate(Policy(), Day, events ?? [], others ?? []);
        return Lease.Grant(device, snapshot, allowance, Now, Day);
    }

    [Fact]
    public void Grant_SetsAsideAllowance_ForTotalAndEachLimitedApp()
    {
        var lease = GrantFor(Phone, Min(5));

        Assert.Equal(Min(5), lease.Total);
        Assert.Equal(Min(5), lease.Apps[YouTube]);
        Assert.False(lease.Apps.ContainsKey(Teams));
        Assert.Equal(Day.End, lease.ExpiresAt);
    }

    [Fact]
    public void Grant_NeverExceedsWhatIsLeft()
    {
        var lease = GrantFor(Phone, Min(10), [Use(YouTube, 27, startHour: 8), Use(Minecraft, 90, startHour: 10)]);

        Assert.Equal(Min(3), lease.Total);
        Assert.Equal(Min(3), lease.Apps[YouTube]);
    }

    [Fact]
    public void Reservation_ReducesWhatOtherDevicesSee()
    {
        var phoneLease = GrantFor(Phone, Min(10));

        var pcView = BudgetCalculator.Calculate(Policy(), Day, [], [phoneLease]);

        Assert.Equal(Min(110), pcView.TotalRemaining);
        Assert.Equal(Min(20), pcView.Remaining(YouTube));
    }

    [Fact]
    public void Reservations_MeanOfflineUseCanNeverExceedTheDailyLimit()
    {
        var phoneLease = GrantFor(Phone, Min(10));
        var pcLease = GrantFor(Pc, Min(60), others: [phoneLease]);
        var pcView = BudgetCalculator.Calculate(Policy(), Day, [], [phoneLease, pcLease]);

        Assert.Equal(Min(120), phoneLease.Total + pcLease.Total + pcView.TotalRemaining);
        Assert.Equal(Min(30), phoneLease.Apps[YouTube] + pcLease.Apps[YouTube] + pcView.Remaining(YouTube)!.Value);
    }

    [Fact]
    public void OfflineUse_StopsWhenSetAsideTimeRunsOut()
    {
        var lease = GrantFor(Phone, Min(5));

        var afterThree = BudgetCalculator.CalculateOffline(lease, Policy().ExemptAppIds, [OfflineUse(Minecraft, 3)]);
        var afterFive = BudgetCalculator.CalculateOffline(lease, Policy().ExemptAppIds, [OfflineUse(Minecraft, 5)]);

        Assert.Equal(Min(2), afterThree.Remaining(Minecraft));
        Assert.True(afterFive.IsBlocked(Minecraft));
        Assert.True(afterFive.IsBlocked(YouTube));
        Assert.False(afterFive.IsBlocked(Teams));
    }

    [Fact]
    public void UnusedSetAsideTime_ReturnsToThePool_WhenDeviceReconnects()
    {
        // Worked example from the README: 10 min set aside on the phone, 6 used offline, 4 come back.
        var phoneLease = GrantFor(Phone, Min(10));
        var offlineUse = OfflineUse(Minecraft, 6);

        var pcViewAfterReconnect = BudgetCalculator.Calculate(Policy(), Day, [offlineUse], []);

        Assert.Equal(Min(114), pcViewAfterReconnect.TotalRemaining);
        Assert.Equal(Min(110), BudgetCalculator.Calculate(Policy(), Day, [], [phoneLease]).TotalRemaining);
    }

    [Fact]
    public void OnlineUseSinceGrant_DrawsDownTheLease_SoGoingOfflineCannotDoubleSpend()
    {
        var lease = GrantFor(Phone, Min(10));
        var usedOnlineAfterGrant = OfflineUse(Minecraft, 8);

        var offline = BudgetCalculator.CalculateOffline(lease, Policy().ExemptAppIds, [usedOnlineAfterGrant]);

        Assert.Equal(Min(2), offline.Remaining(Minecraft));
    }

    [Fact]
    public void ZeroAllowance_BlocksEverythingButExemptAppsAsSoonAsOffline()
    {
        var lease = GrantFor(Phone, TimeSpan.Zero);

        var offline = BudgetCalculator.CalculateOffline(lease, Policy().ExemptAppIds, []);

        Assert.True(offline.IsBlocked(Minecraft));
        Assert.True(offline.IsBlocked(YouTube));
        Assert.True(offline.IsBlocked(null));
        Assert.False(offline.IsBlocked(Teams));
    }

    [Fact]
    public void Lease_IsInactiveAfterTheDailyReset()
    {
        var lease = GrantFor(Phone, Min(5));

        Assert.True(lease.IsActive(Now));
        Assert.False(lease.IsActive(Day.End));
    }

    [Fact]
    public void Grant_RejectsNegativeAllowance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GrantFor(Phone, Min(-1)));
    }

    private static UsageEvent OfflineUse(Guid app, double minutes) =>
        new(Guid.NewGuid(), Phone, app, "raw", Now.AddMinutes(1), TimeSpan.FromMinutes(minutes));
}
