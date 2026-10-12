using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ScreenTime.Core.Models;
using ScreenTime.Server.Data;
using ScreenTime.Server.Services;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

public class PushTests
{
    [Fact]
    public async Task UsageOnOneDevice_IsPushedToTheOthers()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (_, pcPairing) = await factory.PairDeviceAsync(parent, child.Id, "PC");
        var (phone, _) = await factory.PairDeviceAsync(parent, child.Id, "Phone");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, BudgetHub.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(pcPairing.Token);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        var received = new TaskCompletionSource<BudgetSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<BudgetSnapshot>(BudgetHub.BudgetUpdated, snapshot => received.TrySetResult(snapshot));
        await connection.StartAsync();

        await factory.UseAsync(phone, MatcherKindEnum.Domain, "youtube.com", 20);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromMinutes(20), pushed.TotalUsed);
        Assert.Equal(TimeSpan.FromMinutes(5), pushed.Remaining(SeedCatalog.YouTube));
    }
}
