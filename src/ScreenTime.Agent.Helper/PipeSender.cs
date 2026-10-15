using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Channels;
using ScreenTime.Core;
using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Helper;

/// <summary>
/// Sends samples to the Service over its pipe (<see cref="HelperPipe"/>), connecting again whenever the connection is lost.
/// Only the latest sample waits to be sent; older ones are dropped.
/// </summary>
public sealed class PipeSender : IDisposable
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    /// <summary>Exactly what the Service grants the user (<c>HelperPipeServer.ClientRights</c>), so the connection isn't refused.</summary>
    private const PipeAccessRights Rights = PipeAccessRights.WriteData | PipeAccessRights.ReadAttributes | PipeAccessRights.Synchronize;

    private readonly Channel<UsageKey[]> _samples =
        Channel.CreateBounded<UsageKey[]>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public PipeSender(string pipeName = HelperPipe.Name)
    {
        _loop = Task.Run(() => RunAsync(pipeName, _stop.Token));
    }

    public void Send(UsageKey[] inUse) => _samples.Writer.TryWrite(inUse);

    private async Task RunAsync(string pipeName, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                try
                {
                    await using var pipe = new NamedPipeClientStream(".", pipeName, Rights, PipeOptions.Asynchronous,
                        TokenImpersonationLevel.Identification, HandleInheritability.None);
                    await pipe.ConnectAsync(ct);
                    Console.WriteLine("Connected to the Service.");
                    await foreach (var inUse in _samples.Reader.ReadAllAsync(ct))
                        await pipe.WriteAsync(HelperPipe.Encode(inUse), ct);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine($"Not connected to the Service: {e.Message}");
                    await Task.Delay(RetryAfter, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait();
        _stop.Dispose();
    }
}
