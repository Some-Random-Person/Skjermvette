using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace Skjermvette.Agent.Service;

/// <summary>
/// Keeps the Helper running in the session of the user signed in at the computer (see <see cref="HelperSupervisor"/>),
/// and closes it when the service stops. Only as SYSTEM: in a console the Helper is started by hand.
/// </summary>
public sealed class HelperWorker(IConfiguration configuration, HelperProcess running, TimeProvider time, ILogger<HelperWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// <c>Agent:HelperPath</c> from configuration, or the Helper in a <c>Helper</c> folder next to the service. Its own
    /// folder: published self-contained (as the installer does), their copies of some runtime files differ.
    /// </summary>
    public static string HelperPath(IConfiguration configuration) =>
        configuration["Agent:HelperPath"] is { Length: > 0 } path
            ? Environment.ExpandEnvironmentVariables(path)
            : Path.Combine(AppContext.BaseDirectory, "Helper", "Skjermvette.Agent.Helper.exe");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!WindowsIdentity.GetCurrent().IsSystem)
        {
            logger.LogWarning("Not running as SYSTEM, so the Helper isn't started. Start it by hand: dotnet run --project src/Skjermvette.Agent.Helper");
            return;
        }

        var path = HelperPath(configuration);
        var supervisor = new HelperSupervisor(time);
        Process? helper = null;
        var session = 0;
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            do
            {
                if (helper is { HasExited: true })
                {
                    // No exit code: the process wasn't started through Process, which then can't read it.
                    logger.LogWarning("The Helper stopped.");
                    running.Stopped();
                    helper.Dispose();
                    helper = null;
                }

                var userSession = ConsoleSession.User() is null ? null : ConsoleSession.Id();
                switch (supervisor.Next(userSession, helper is null ? null : session))
                {
                    case HelperActionEnum.Start:
                        session = userSession!.Value;
                        // One Helper per session: one left from before (or started by hand) would make the new one quit.
                        Enforcer.CloseProcesses(Path.GetFileName(path), session);
                        try
                        {
                            helper = HelperLauncher.Start(path, session);
                            if (helper is null)
                                logger.LogWarning("The Helper stopped as soon as it was started in session {Session}.", session);
                            else
                            {
                                running.Started(helper.Id);
                                logger.LogInformation("Started the Helper in session {Session}.", session);
                            }
                        }
                        catch (Win32Exception e)
                        {
                            supervisor.Failed();
                            logger.LogError("Can't start the Helper ({Path}) in session {Session}: {Message} Trying again in {Delay}.",
                                path, session, e.Message, supervisor.Delay);
                        }
                        break;
                    case HelperActionEnum.Stop:
                        logger.LogInformation("Another user is signed in. Closing the Helper in session {Session}.", session);
                        Close(helper!);
                        helper = null;
                        break;
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (helper is not null)
                Close(helper);
        }
    }

    private void Close(Process helper)
    {
        running.Stopped();
        using (helper)
        {
            try
            {
                helper.Kill();
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            {
                // Already gone.
            }
        }
    }
}
