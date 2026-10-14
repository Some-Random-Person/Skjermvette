using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ScreenTime.Server.Auth;

namespace ScreenTime.Server.Services;

/// <summary>Devices connect here to get their <c>BudgetSnapshot</c> pushed whenever it changes.</summary>
[Authorize(Policy = AppClaims.DevicePolicy)]
public class BudgetHub(BudgetService budget) : Hub
{
    public const string Path = "/hubs/budget";
    public const string BudgetUpdated = "BudgetUpdated";

    public static string DeviceGroup(Guid deviceId) => $"device:{deviceId}";

    /// <summary>
    /// Joins the device's group, then sends its current budget. The client sees the connection as open
    /// before this runs, so anything that changed in between would otherwise be missed until the next
    /// change; sending the budget here means a (re)connected device is always up to date.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var deviceId = Context.User!.GetDeviceId();
        await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));

        var day = await budget.LoadDayAsync(Context.User!.GetChildId(), Context.ConnectionAborted);
        await Clients.Caller.SendAsync(BudgetUpdated, day.SnapshotFor(deviceId), Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }
}
