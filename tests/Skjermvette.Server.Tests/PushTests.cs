using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Skjermvette.Core.Models;
using Skjermvette.Server.Data;
using Skjermvette.Server.Services;
using static Skjermvette.Server.Tests.ServerFactory;

namespace Skjermvette.Server.Tests;

public class PushTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Connecting_SendsTheCurrentBudgetRightAway()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (pc, pcPairing) = await factory.PairDeviceAsync(parent, child.Id, "PC");
        await factory.UseAsync(pc, MatcherKindEnum.Domain, "youtube.com", 12);

        var (connection, pushes) = await ConnectAsync(factory, pcPairing.Token);
        await using var _ = connection;
        var first = await pushes.ReadAsync().AsTask().WaitAsync(Wait);

        Assert.Equal(TimeSpan.FromMinutes(12), first.TotalUsed);
    }

    [Fact]
    public async Task UsageOnOneDevice_IsPushedToTheOthers()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (_, pcPairing) = await factory.PairDeviceAsync(parent, child.Id, "PC");
        var (phone, _) = await factory.PairDeviceAsync(parent, child.Id, "Phone");

        var (connection, pushes) = await ConnectAsync(factory, pcPairing.Token);
        await using var _ = connection;
        // The budget sent on connect proves the PC has joined its group, so the next change can't be missed.
        await pushes.ReadAsync().AsTask().WaitAsync(Wait);

        await factory.UseAsync(phone, MatcherKindEnum.Domain, "youtube.com", 20);
        var pushed = await pushes.ReadAsync().AsTask().WaitAsync(Wait);

        Assert.Equal(TimeSpan.FromMinutes(20), pushed.TotalUsed);
        Assert.Equal(TimeSpan.FromMinutes(5), pushed.Remaining(SeedCatalog.YouTube));
    }

    private static async Task<(HubConnection Connection, ChannelReader<BudgetSnapshot> Pushes)> ConnectAsync(ServerFactory factory, string token)
    {
        var pushes = Channel.CreateUnbounded<BudgetSnapshot>();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, BudgetHub.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        connection.On<BudgetSnapshot>(BudgetHub.BudgetUpdated, snapshot => pushes.Writer.TryWrite(snapshot));
        await connection.StartAsync();
        return (connection, pushes.Reader);
    }
}
