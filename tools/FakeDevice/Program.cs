using FakeDevice;
using ScreenTime.Core.DeviceApi;
using ScreenTime.Core.Models;

const string Help = """
    Fake device for ScreenTime: pairs like a real device and reports made-up usage.

    Usage:
      pair <code> --name <name> [--platform Windows|Android|Ios|MacOs|Linux] [--server <url>]
          Pair with a 6-digit code from the dashboard (Devices tab). Default server: http://localhost:5131
      use <name> <kind> <identifier> <minutes>
          Report <minutes> of use that ended just now, e.g.  use Phone package com.google.android.youtube 15
      live <name> <kind> <identifier>
          Keep using an app: reports 15 s every 15 s (like a real agent) and shows pushed updates.
          Stops reporting when the app is blocked. Ctrl+C to quit.
      state <name>      Show the device's current budget and offline allowance.
      watch <name>      Show budget updates as the server pushes them. Ctrl+C to quit.
      list              List paired fake devices.
      forget <name>     Delete a fake device's saved token (unpair it in the dashboard too).

    Kinds: program (process), website (domain), android (package), apple (bundle), iostoken.
    """;

try
{
    return args switch
    {
        ["pair", var code, .. var options] => await PairAsync(code, options),
        ["use", var name, var kind, var identifier, var minutes] => await UseAsync(name, ParseKind(kind), identifier, ParseMinutes(minutes)),
        ["live", var name, var kind, var identifier] => await LiveAsync(name, ParseKind(kind), identifier),
        ["state", var name] => await StateAsync(name),
        ["watch", var name] => await WatchAsync(name),
        ["list"] => List(),
        ["forget", var name] => Forget(name),
        _ => ShowHelp(),
    };
}
catch (UsageException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

static int ShowHelp()
{
    Console.WriteLine(Help);
    return 2;
}

static async Task<int> PairAsync(string code, string[] options)
{
    var name = Option(options, "--name") ?? throw new UsageException("Give the device a name with --name, e.g. --name Phone.");
    var platform = Option(options, "--platform") is { } p
        ? Enum.TryParse<DevicePlatformEnum>(p, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new UsageException($"Unknown platform \"{p}\". Use Windows, Android, Ios, MacOs or Linux.")
        : DevicePlatformEnum.Android;
    var server = new Uri(Option(options, "--server") ?? "http://localhost:5131");

    var paired = await DeviceClient.PairAsync(server, new PairRequest(code, name, platform));
    await DeviceStore.SaveAsync(new SavedDevice(name, server, paired.DeviceId, paired.ChildId, paired.Token));
    Console.WriteLine($"Paired \"{name}\" ({platform}). It now shows on the child's Devices tab.");
    return 0;
}

static async Task<int> UseAsync(string name, MatcherKindEnum kind, string identifier, double minutes)
{
    using var client = new DeviceClient(await DeviceStore.LoadAsync(name));
    var end = DateTimeOffset.UtcNow;
    var events = new List<UsageEventDto>();
    // Events may be at most an hour long; 5-minute pieces look like what an agent sends.
    for (var done = 0.0; done < minutes; done += 5)
    {
        var length = Math.Min(5, minutes - done);
        events.Add(new UsageEventDto(Guid.NewGuid(), kind, identifier, end.AddMinutes(done - minutes), length * 60));
    }

    var sync = await client.SyncAsync(events);
    var catalog = (await client.StateAsync()).Catalog;
    Console.WriteLine($"Reported {minutes:0.##} min of {identifier}.");
    Report.Budget(sync.Snapshot, catalog);
    Report.Lease(sync.Lease, catalog);
    return 0;
}

static async Task<int> LiveAsync(string name, MatcherKindEnum kind, string identifier)
{
    var device = await DeviceStore.LoadAsync(name);
    using var client = new DeviceClient(device);
    var state = await client.StateAsync();
    var app = new ScreenTime.Core.AppResolver(state.Catalog).Resolve(kind, identifier);
    Console.WriteLine($"Using {identifier} ({app?.Name ?? "not a known app; counts toward the total"}) on \"{device.Name}\". Ctrl+C to stop.");

    await using var hub = await client.ConnectAsync(snapshot =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] pushed: {Status(snapshot, app)}"));

    using var stop = CancelOnCtrlC();
    var interval = TimeSpan.FromSeconds(15);
    var blocked = false;
    while (!stop.IsCancellationRequested)
    {
        // A real agent stops counting once the app is blocked, but keeps checking in.
        var events = blocked ? [] : new[] { new UsageEventDto(Guid.NewGuid(), kind, identifier, DateTimeOffset.UtcNow - interval, interval.TotalSeconds) };
        var sync = await client.SyncAsync(events);
        blocked = sync.Snapshot.IsBlocked(app?.Id);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {(events.Length > 0 ? "reported 15 s" : "checked in")}: {Status(sync.Snapshot, app)}");
        await Delay(interval, stop.Token);
    }
    return 0;
}

static async Task<int> StateAsync(string name)
{
    using var client = new DeviceClient(await DeviceStore.LoadAsync(name));
    var state = await client.StateAsync();
    Report.Budget(state.Snapshot, state.Catalog);
    if (state.Lease is { } lease)
        Report.Lease(lease, state.Catalog);
    else
        Console.WriteLine("Offline allowance: none yet (granted at the first check-in).");
    return 0;
}

static async Task<int> WatchAsync(string name)
{
    var device = await DeviceStore.LoadAsync(name);
    using var client = new DeviceClient(device);
    var catalog = (await client.StateAsync()).Catalog;
    await using var hub = await client.ConnectAsync(snapshot =>
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] budget for \"{device.Name}\":");
        Report.Budget(snapshot, catalog);
    });
    Console.WriteLine($"Watching \"{device.Name}\" for pushed updates. Ctrl+C to stop.");

    using var stop = CancelOnCtrlC();
    await Delay(Timeout.InfiniteTimeSpan, stop.Token);
    return 0;
}

static int List()
{
    var devices = DeviceStore.All().ToList();
    if (devices.Count == 0)
        Console.WriteLine("No fake devices yet. Pair one with 'pair'.");
    foreach (var device in devices)
        Console.WriteLine($"{device.Name,-12} {device.Server}");
    return 0;
}

static int Forget(string name)
{
    Console.WriteLine(DeviceStore.Forget(name) ? $"Forgot \"{name}\"." : $"No fake device called \"{name}\".");
    return 0;
}

static string Status(BudgetSnapshot snapshot, LogicalApp? app)
{
    var total = $"total {Report.Duration(snapshot.TotalRemaining)} left";
    if (app is null)
        return snapshot.IsBlocked(null) ? "BLOCKED (total used up)" : total;
    if (snapshot.Remaining(app.Id) is not { } remaining)
        return $"{app.Name} is exempt · {total}";
    return remaining <= TimeSpan.Zero ? $"{app.Name} BLOCKED · {total}" : $"{app.Name} {Report.Duration(remaining)} left · {total}";
}

static MatcherKindEnum ParseKind(string kind) => kind.ToLowerInvariant() switch
{
    "program" or "process" => MatcherKindEnum.Process,
    "website" or "domain" or "site" => MatcherKindEnum.Domain,
    "android" or "package" => MatcherKindEnum.Package,
    "apple" or "bundle" => MatcherKindEnum.Bundle,
    "iostoken" => MatcherKindEnum.IosToken,
    _ => throw new UsageException($"Unknown kind \"{kind}\". Use program, website, android, apple or iostoken."),
};

static double ParseMinutes(string minutes) =>
    double.TryParse(minutes, System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
        ? value
        : throw new UsageException($"\"{minutes}\" isn't a positive number of minutes.");

static string? Option(string[] options, string name)
{
    var i = Array.FindIndex(options, o => o.Equals(name, StringComparison.OrdinalIgnoreCase));
    return i >= 0 && i + 1 < options.Length ? options[i + 1] : null;
}

static CancellationTokenSource CancelOnCtrlC()
{
    var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        stop.Cancel();
    };
    return stop;
}

static async Task Delay(TimeSpan delay, CancellationToken ct)
{
    try
    {
        await Task.Delay(delay, ct);
    }
    catch (OperationCanceledException)
    {
        // Ctrl+C.
    }
}
