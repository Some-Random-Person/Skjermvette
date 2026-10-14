using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Tests;

/// <summary>Runs the real server against a throwaway SQLite file, with a controllable clock.</summary>
public class ServerFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"screentime-test-{Guid.NewGuid():N}.db");

    // 12 Oct 2026, 10:00 UTC. Children in tests use UTC with a midnight reset.
    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // No pooling: the file can be deleted as soon as the server stops, without clearing pools.
        // SqliteConnection.ClearAllPools() is process-wide and closed connections other tests (running in
        // parallel) were still using, which made random tests fail with a 500 under load (#22).
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
        builder.ConfigureLogging(logging => logging.AddProvider(new ServerErrorLog()));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Windows (e.g. a virus scanner) can hold the file for a moment. It's a temp file, so leave it
            // rather than failing a test that passed.
        }
    }

    public async Task<HttpClient> CreateParentAsync(string email = "parent@example.com")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "correct-horse-battery"), Json);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>2 h total, YouTube 30 min, Teams exempt: the scenario from the requirements.</summary>
    public static ChildRequest DefaultChild(int offlineAllowanceMinutes = 5) => new(
        "Alex",
        "UTC",
        0,
        120,
        offlineAllowanceMinutes,
        [
            new AppRuleDto(SeedCatalog.YouTube, AppRuleKindEnum.Limited, 30),
            new AppRuleDto(SeedCatalog.Teams, AppRuleKindEnum.Exempt, null),
        ]);

    public static async Task<ChildResponse> CreateChildAsync(HttpClient parent, ChildRequest? request = null)
    {
        var response = await parent.PostAsJsonAsync("/api/children", request ?? DefaultChild(), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChildResponse>(Json))!;
    }

    public async Task<(HttpClient Client, PairResponse Pairing)> PairDeviceAsync(HttpClient parent, Guid childId, string name = "PC")
    {
        var code = await (await parent.PostAsync($"/api/children/{childId}/pairing-codes", null)).Content.ReadFromJsonAsync<PairingCodeResponse>(Json);
        var response = await CreateClient().PostAsJsonAsync("/api/device/pair", new PairRequest(code!.Code, name, DevicePlatformEnum.Windows), Json);
        response.EnsureSuccessStatusCode();
        var pairing = (await response.Content.ReadFromJsonAsync<PairResponse>(Json))!;

        var device = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pairing.Token);
        return (device, pairing);
    }

    // Where the next UseAsync call without a start goes. Overlapping time counts once, so by default
    // each call is placed right after the previous one, starting three hours before "now".
    private DateTimeOffset? _nextStart;

    /// <summary>
    /// Uploads <paramref name="minutes"/> of use in 5-minute events starting at <paramref name="start"/>.
    /// Without a start, consecutive calls follow each other so they don't overlap.
    /// </summary>
    public async Task<SyncResponse> UseAsync(HttpClient device, MatcherKindEnum kind, string raw, double minutes, DateTimeOffset? start = null)
    {
        var from = start ?? _nextStart ?? Clock.Now.AddHours(-3);
        if (start is null)
            _nextStart = from.AddMinutes(minutes);
        var events = new List<UsageEventDto>();
        for (var done = 0.0; done < minutes; done += 5)
            events.Add(new UsageEventDto(Guid.NewGuid(), kind, raw, from.AddMinutes(done), Math.Min(5, minutes - done) * 60));

        var response = await device.PostAsJsonAsync("/api/device/usage", new UsageBatchRequest(events), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SyncResponse>(Json))!;
    }

    public static async Task<DeviceStateResponse> StateAsync(HttpClient device) =>
        (await device.GetFromJsonAsync<DeviceStateResponse>("/api/device/state", Json))!;
}
