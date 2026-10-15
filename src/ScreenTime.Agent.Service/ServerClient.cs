using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Service;

/// <summary>
/// The server's device API. Throws <see cref="UnpairedException"/> on 401, <see cref="ServerRejectedException"/> on 400,
/// and <see cref="HttpRequestException"/> when the server can't be reached or fails (the device is offline).
/// </summary>
public sealed class ServerClient : IDisposable
{
    // Same as the server: web defaults, enums as strings.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private const string HubPath = "/hubs/budget";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly DeviceIdentity _identity;
    private readonly Func<HttpMessageHandler>? _createHandler;
    private readonly HttpClient _http;

    /// <param name="createHandler">Only for tests, to talk to an in-memory server. A new handler each time: SignalR disposes its own.</param>
    public ServerClient(DeviceIdentity identity, Func<HttpMessageHandler>? createHandler = null)
    {
        _identity = identity;
        _createHandler = createHandler;
        _http = NewHttpClient(identity.Server, createHandler?.Invoke());
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", identity.Token);
    }

    public static async Task<PairResponse> PairAsync(Uri server, PairRequest request)
    {
        using var http = NewHttpClient(server, null);
        return await SendAsync<PairResponse>(ct => http.PostAsJsonAsync("/api/device/pair", request, Json, ct), CancellationToken.None);
    }

    public Task<DeviceStateResponse> StateAsync(CancellationToken ct) =>
        SendAsync<DeviceStateResponse>(c => _http.GetAsync("/api/device/state", c), ct);

    /// <summary>Uploads usage and renews the offline allowance.</summary>
    public Task<SyncResponse> SyncAsync(IReadOnlyList<UsageEventDto> events, CancellationToken ct) =>
        SendAsync<SyncResponse>(c => _http.PostAsJsonAsync("/api/device/usage", new UsageBatchRequest(events), Json, c), ct);

    /// <summary>A connection to the budget hub (not started yet); <paramref name="onUpdate"/> runs for every pushed budget.</summary>
    public HubConnection CreateHub(Action<BudgetSnapshot> onUpdate)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_identity.Server, HubPath), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_identity.Token);
                if (_createHandler is not null)
                {
                    // The in-memory test server has no WebSockets.
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => _createHandler();
                }
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .WithAutomaticReconnect()
            .Build();
        connection.On("BudgetUpdated", onUpdate);
        return connection;
    }

    public void Dispose() => _http.Dispose();

    private static HttpClient NewHttpClient(Uri server, HttpMessageHandler? handler) =>
        new(handler ?? new HttpClientHandler()) { BaseAddress = server, Timeout = Timeout };

    private static async Task<T> SendAsync<T>(Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await send(ct);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException("The server didn't answer in time.", e);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;

            var body = await response.Content.ReadAsStringAsync(ct);
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new UnpairedException(),
                HttpStatusCode.BadRequest => new ServerRejectedException(ProblemText(body)),
                _ => new HttpRequestException($"Server said {(int)response.StatusCode}: {body}", null, response.StatusCode),
            };
        }
    }

    /// <summary>The readable part of a ProblemDetails reply: its validation messages or its detail.</summary>
    private static string ProblemText(string body)
    {
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            if (root.TryGetProperty("errors", out var errors))
                return string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).Select(m => m.GetString()));
            if (root.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? body;
        }
        catch (JsonException)
        {
            // Not JSON: show it as it is.
        }
        return body;
    }
}
