using System.Net;
using System.Net.Http.Json;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

public class ChildTests
{
    [Fact]
    public async Task CreateChild_StoresRulesAndDefaultOfflineAllowance()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();

        var child = await CreateChildAsync(parent);

        Assert.Equal(120, child.DailyTotalMinutes);
        Assert.Equal(5, child.OfflineAllowanceMinutes);
        Assert.Contains(child.Rules, r => r.AppId == SeedCatalog.YouTube && r.LimitMinutes == 30);
        Assert.Contains(child.Rules, r => r.AppId == SeedCatalog.Teams && r.Kind == AppRuleKindEnum.Exempt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task OfflineAllowance_OutsideZeroToSixty_IsRejected(int minutes)
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();

        var response = await parent.PostAsJsonAsync("/api/children", DefaultChild(minutes), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("OfflineAllowanceMinutes", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AppBothLimitedAndExempt_IsRejected()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var request = DefaultChild() with
        {
            Rules = [new(SeedCatalog.YouTube, AppRuleKindEnum.Limited, 30), new(SeedCatalog.YouTube, AppRuleKindEnum.Exempt, null)],
        };

        var response = await parent.PostAsJsonAsync("/api/children", request, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTimeZone_IsRejected()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();

        var response = await parent.PostAsJsonAsync("/api/children", DefaultChild() with { TimeZoneId = "Mars/Olympus" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChild_ReplacesRules()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);

        var update = DefaultChild() with { DailyTotalMinutes = 90, Rules = [new(SeedCatalog.Roblox, AppRuleKindEnum.Limited, 15)] };
        (await parent.PutAsJsonAsync($"/api/children/{child.Id}", update, Json)).EnsureSuccessStatusCode();

        var stored = await parent.GetFromJsonAsync<ChildResponse>($"/api/children/{child.Id}", Json);
        Assert.Equal(90, stored!.DailyTotalMinutes);
        Assert.Equal([new AppRuleDto(SeedCatalog.Roblox, AppRuleKindEnum.Limited, 15)], stored.Rules);
    }

    [Fact]
    public async Task UpdateChild_ChangingTheLimitOfTheSameApp_Works()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);

        var update = DefaultChild() with
        {
            Rules = [new(SeedCatalog.YouTube, AppRuleKindEnum.Limited, 45), new(SeedCatalog.Teams, AppRuleKindEnum.Exempt, null)],
        };
        var response = await parent.PutAsJsonAsync($"/api/children/{child.Id}", update, Json);

        response.EnsureSuccessStatusCode();
        var stored = await parent.GetFromJsonAsync<ChildResponse>($"/api/children/{child.Id}", Json);
        Assert.Contains(stored!.Rules, r => r.AppId == SeedCatalog.YouTube && r.LimitMinutes == 45);
    }

    [Fact]
    public async Task ExtraTime_AddsToTodaysTotalOnly()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);

        (await parent.PostAsJsonAsync($"/api/children/{child.Id}/extra-time", new ExtraTimeRequest(15), Json)).EnsureSuccessStatusCode();
        var today = await parent.GetFromJsonAsync<TodayResponse>($"/api/children/{child.Id}/today", Json);
        factory.Clock.Advance(TimeSpan.FromDays(1));
        var tomorrow = await parent.GetFromJsonAsync<TodayResponse>($"/api/children/{child.Id}/today", Json);

        Assert.Equal(135, today!.TotalLimitMinutes);
        Assert.Equal(15, today.ExtraMinutes);
        Assert.Equal(120, tomorrow!.TotalLimitMinutes);
    }

    [Fact]
    public async Task Today_ShowsUsagePerAppIncludingExemptAndOther()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (device, _) = await factory.PairDeviceAsync(parent, child.Id);

        await factory.UseAsync(device, Core.Models.MatcherKindEnum.Domain, "www.youtube.com", 10);
        await factory.UseAsync(device, Core.Models.MatcherKindEnum.Process, "ms-teams.exe", 20);
        await factory.UseAsync(device, Core.Models.MatcherKindEnum.Process, "notepad.exe", 5);
        var today = await parent.GetFromJsonAsync<TodayResponse>($"/api/children/{child.Id}/today", Json);

        Assert.Equal(15, today!.TotalUsedMinutes, 3);
        Assert.Equal(105, today.TotalRemainingMinutes, 3);
        Assert.Contains(today.Apps, a => a.AppId == SeedCatalog.YouTube && a.UsedMinutes == 10 && a.RemainingMinutes == 20);
        Assert.Contains(today.Apps, a => a.AppId == SeedCatalog.Teams && a.UsedMinutes == 20 && a.RemainingMinutes == null);
        Assert.Contains(today.Apps, a => a.AppId == null && a.UsedMinutes == 5);
        Assert.Single(today.Devices, d => d.IsOnline && d.SetAsideMinutes == 5);
    }
}
