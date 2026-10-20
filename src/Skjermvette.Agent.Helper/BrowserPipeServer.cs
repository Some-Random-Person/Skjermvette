using System.IO.Pipes;

namespace Skjermvette.Agent.Helper;

/// <summary>
/// Receives the browser extension's reports from the native messaging host (<see cref="BrowserHost"/>), one connection per
/// browser, and keeps the latest report from each. A browser's report is forgotten when its connection closes, and a
/// connection sending anything but valid reports is closed.
/// </summary>
public sealed class BrowserPipeServer : IDisposable
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    private readonly Dictionary<int, BrowserReport> _reports = [];
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public BrowserPipeServer(string pipeName)
    {
        _loop = Task.Run(() => AcceptAsync(pipeName, _stop.Token));
    }

    /// <summary>The latest report from each connected browser.</summary>
    public IReadOnlyCollection<BrowserReport> Reports()
    {
        lock (_lock)
            return _reports.Values.ToList();
    }

    private async Task AcceptAsync(string pipeName, CancellationToken ct)
    {
        var connections = new List<Task>();
        try
        {
            for (var id = 0; ; id++)
            {
                NamedPipeServerStream pipe;
                try
                {
                    // The default security lets this user (both ends run as the child) connect; other users may only read.
                    pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message, PipeOptions.Asynchronous, BrowserPipe.MaxMessageBytes, 0);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine($"Can't listen for the browser extension: {e.Message}");
                    await Task.Delay(RetryAfter, ct);
                    continue;
                }

                try
                {
                    await pipe.WaitForConnectionAsync(ct);
                }
                catch
                {
                    await pipe.DisposeAsync();
                    throw;
                }
                connections.RemoveAll(c => c.IsCompleted);
                connections.Add(ReadAsync(pipe, id, ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        await Task.WhenAll(connections);
    }

    private async Task ReadAsync(NamedPipeServerStream pipe, int id, CancellationToken ct)
    {
        await using (pipe)
        {
            var buffer = new byte[BrowserPipe.MaxMessageBytes];
            try
            {
                while (true)
                {
                    var read = await pipe.ReadAsync(buffer, ct);
                    if (read == 0 || !pipe.IsMessageComplete || BrowserPipe.Decode(buffer.AsSpan(0, read)) is not { } report)
                        break;
                    lock (_lock)
                        _reports[id] = report;
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException)
            {
                // Disconnected, or the Helper is stopping.
            }
            finally
            {
                lock (_lock)
                    _reports.Remove(id);
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait();
        _stop.Dispose();
    }
}
