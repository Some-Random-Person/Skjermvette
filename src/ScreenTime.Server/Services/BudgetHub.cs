using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ScreenTime.Server.Auth;

namespace ScreenTime.Server.Services;

/// <summary>Devices connect here to get their <c>BudgetSnapshot</c> pushed whenever it changes.</summary>
[Authorize(Policy = AppClaims.DevicePolicy)]
public class BudgetHub : Hub
{
    public const string Path = "/hubs/budget";
    public const string BudgetUpdated = "BudgetUpdated";

    public static string DeviceGroup(Guid deviceId) => $"device:{deviceId}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(Context.User!.GetDeviceId()));
        await base.OnConnectedAsync();
    }
}
