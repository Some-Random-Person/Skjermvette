using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using ScreenTime.Core;

namespace ScreenTime.Agent.Service;

/// <summary>
/// Receives what is in use from the Helper (<see cref="HelperPipe"/>) and feeds it to the <see cref="UsageTracker"/>,
/// stamped with this service's clock. Only SYSTEM and the user signed in at the computer can open the pipe; when that
/// user changes, the pipe is closed and opened again for the new one. One Helper at a time: a new connection replaces
/// the previous one.
/// </summary>
public sealed class HelperPipeServer(UsageTracker tracker, TimeProvider time, ILogger<HelperPipeServer> logger, string pipeName = HelperPipe.Name)
    : BackgroundService
{
    /// <summary>
    /// What the user may do with the pipe: connect and write. Not <c>GENERIC_WRITE</c>, which would also let them create
    /// instances of it. The Helper asks for exactly these rights.
    /// </summary>
    public const PipeAccessRights ClientRights = PipeAccessRights.WriteData | PipeAccessRights.ReadAttributes | PipeAccessRights.Synchronize;

    public static readonly TimeSpan UserCheck = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (ConsoleSession.User() is not { } user)
            {
                await Task.Delay(UserCheck, stoppingToken);
                continue;
            }

            using var userChanged = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var watch = WatchUserAsync(user, userChanged);
            try
            {
                await ListenAsync(user, userChanged.Token);
            }
            catch (OperationCanceledException) when (userChanged.IsCancellationRequested)
            {
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Most likely another program created the pipe first.
                logger.LogError("Can't open the Helper's pipe {Pipe}: {Message}", pipeName, e.Message);
                await Task.Delay(UserCheck, stoppingToken);
            }
            await userChanged.CancelAsync();
            await watch;
        }
    }

    private static async Task WatchUserAsync(SecurityIdentifier user, CancellationTokenSource userChanged)
    {
        try
        {
            while (ConsoleSession.User() == user)
                await Task.Delay(UserCheck, userChanged.Token);
            await userChanged.CancelAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ListenAsync(SecurityIdentifier user, CancellationToken ct)
    {
        var security = Security(user);
        var options = PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance;
        CancellationTokenSource? servingCts = null;
        var serving = Task.CompletedTask;
        try
        {
            while (true)
            {
                // An instance is always open, so the pipe's name is never free for another program to take.
                var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message, options, HelperPipe.MaxMessageBytes, HelperPipe.MaxMessageBytes, security);
                options &= ~PipeOptions.FirstPipeInstance;
                try
                {
                    await pipe.WaitForConnectionAsync(ct);
                }
                catch
                {
                    await pipe.DisposeAsync();
                    throw;
                }

                await StopAsync(servingCts, serving);
                servingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                serving = ServeAsync(pipe, servingCts.Token);
            }
        }
        finally
        {
            await StopAsync(servingCts, serving);
        }
    }

    private static async Task StopAsync(CancellationTokenSource? servingCts, Task serving)
    {
        if (servingCts is null)
            return;
        await servingCts.CancelAsync();
        await serving;
        servingCts.Dispose();
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        await using var _ = pipe;
        logger.LogInformation("Helper connected.");
        var buffer = new byte[HelperPipe.MaxMessageBytes];
        try
        {
            while (true)
            {
                var read = await pipe.ReadAsync(buffer, ct);
                if (read == 0)
                    break;
                if (!pipe.IsMessageComplete || HelperPipe.Decode(buffer.AsSpan(0, read)) is not { } inUse)
                {
                    logger.LogWarning("The Helper sent something that isn't a sample. Disconnecting it.");
                    break;
                }
                tracker.Observe(time.GetUtcNow(), inUse);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
            // The Helper went away.
        }
        logger.LogInformation("Helper disconnected.");
    }

    /// <summary>SYSTEM and this service's own account (the same once installed) have full control; the user gets <see cref="ClientRights"/>.</summary>
    private static PipeSecurity Security(SecurityIdentifier user)
    {
        var security = new PipeSecurity();
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        foreach (var owner in new[] { system, WindowsIdentity.GetCurrent().User! }.Distinct())
            security.AddAccessRule(new PipeAccessRule(owner, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(user, ClientRights, AccessControlType.Allow));
        return security;
    }
}
