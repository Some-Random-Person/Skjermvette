namespace ScreenTime.Agent.Service;

/// <summary>Checks in with the server every 15 seconds, for as long as the service runs and the device stays paired.</summary>
public sealed class SyncWorker(
    IConfiguration configuration,
    UsageTracker tracker,
    BudgetState state,
    TimeProvider time,
    ILogger<SyncWorker> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var folder = AgentFolder.Create(configuration);
        if (new DeviceIdentityStore(folder).Load() is not { } identity)
        {
            Stop($"This computer isn't paired. Pair it with: {Commands.PairUsage}");
            return;
        }

        await using var sync = new DeviceSync(identity, folder, tracker, state, time, logger);
        logger.LogInformation("Paired with {Server} as device {DeviceId}.", identity.Server, identity.DeviceId);
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            do
            {
                await sync.TickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: keep what was used since the last check-in for next time.
            await sync.SaveUsageAsync(CancellationToken.None);
        }
        catch (UnpairedException e)
        {
            Stop(e.Message);
        }
    }

    private void Stop(string message)
    {
        logger.LogCritical("{Message}", message);
        Environment.ExitCode = 1;
        lifetime.StopApplication();
    }
}
