using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;

namespace FakeDevice;

/// <summary>Talks to the server's device API the same way a real agent does.</summary>
public sealed class DeviceClient : IDisposable
{
    // Same as the server: web defaults, enums as strings.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    private const string HubPath = "/hubs/budget";
    private readonly HttpClient _http;
    private readonly SavedDevice _device;

    public DeviceClient(SavedDevice device)
    {
        _device = device;
        _http = new HttpClient { BaseAddress = device.Server };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", device.Token);
    }

    public static async Task<PairResponse> PairAsync(Uri server, PairRequest request)
    {
        using var http = new HttpClient { BaseAddress = server };
        return await ReadAsync<PairResponse>(await SendAsync(() => http.PostAsJsonAsync("/api/device/pair", request, Json)));
    }

    public async Task<DeviceStateResponse> StateAsync() =>
        await ReadAsync<DeviceStateResponse>(await SendAsync(() => _http.GetAsync("/api/device/state")));

    /// <summary>Uploads usage and renews the offline allowance, like an agent's regular check-in.</summary>
    public async Task<SyncResponse> SyncAsync(IReadOnlyList<UsageEventDto> events) =>
        await ReadAsync<SyncResponse>(await SendAsync(() => _http.PostAsJsonAsync("/api/device/usage", new UsageBatchRequest(events), Json)));

    /// <summary>Connects to the budget hub; <paramref name="onUpdate"/> runs for every pushed budget.</summary>
    public async Task<HubConnection> ConnectAsync(Action<BudgetSnapshot> onUpdate)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_device.Server, HubPath), o => o.AccessTokenProvider = () => Task.FromResult<string?>(_device.Token))
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .WithAutomaticReconnect()
            .Build();
        connection.On("BudgetUpdated", onUpdate);
        await connection.StartAsync();
        return connection;
    }

    public void Dispose() => _http.Dispose();

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException e)
        {
            throw new UsageException($"Can't reach the server ({e.Message}). Is it running?");
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<T>(Json))!;

        var detail = await ProblemMessageAsync(response);
        throw new UsageException(response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            ? "The server doesn't accept this device's token. It may have been unpaired; pair it again."
            : $"Server said {(int)response.StatusCode}: {detail}");
    }

    private static async Task<string> ProblemMessageAsync(HttpResponseMessage response)
    {
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = problem.RootElement;
            if (root.TryGetProperty("errors", out var errors))
                return string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).Select(m => m.GetString()));
            if (root.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? "";
            return root.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
            return response.ReasonPhrase ?? "";
        }
    }
}
