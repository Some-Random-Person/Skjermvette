using System.IO.Pipes;
using System.Threading.Channels;
using Skjermvette.Core;

namespace Skjermvette.Agent.Helper;

/// <summary>
/// Receives the browser extension's reports from the native messaging host (<see cref="BrowserHost"/>), one connection per
/// browser, and keeps the latest report from each. A browser's report is forgotten when its connection closes, and a
/// connection sending anything but valid reports is closed. Each browser is sent the websites to block when it connects
/// and whenever they change (<see cref="Block"/>).
/// </summary>
public sealed class BrowserPipeServer : IDisposable
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    private readonly Dictionary<int, BrowserReport> _reports = [];
    /// <summary>What waits to be sent to each connected browser: only the latest websites to block.</summary>
    private readonly Dictionary<int, Channel<byte[]>> _outgoing = [];
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private byte[]? _websites;

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

    /// <summary>Sends the websites to block to every connected browser, and to browsers connecting later.</summary>
    public void Block(WebsiteBlocks websites)
    {
        var message = BrowserPipe.Encode(websites);
        lock (_lock)
        {
            _websites = message;
            foreach (var outgoing in _outgoing.Values)
                outgoing.Writer.TryWrite(message);
        }
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
                    pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
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

                var outgoing = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
                lock (_lock)
                {
                    _outgoing[id] = outgoing;
                    if (_websites is not null)
                        outgoing.Writer.TryWrite(_websites);
                }
                connections.RemoveAll(c => c.IsCompleted);
                connections.Add(ServeAsync(pipe, id, outgoing.Reader, ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        await Task.WhenAll(connections);
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, int id, ChannelReader<byte[]> outgoing, CancellationToken ct)
    {
        await using (pipe)
        {
            using var closed = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var writing = WriteAsync(pipe, outgoing, closed.Token);
            await ReadAsync(pipe, id, ct);
            await closed.CancelAsync();
            await writing;
            lock (_lock)
            {
                _reports.Remove(id);
                _outgoing.Remove(id);
            }
        }
    }

    private async Task ReadAsync(NamedPipeServerStream pipe, int id, CancellationToken ct)
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
    }

    private static async Task WriteAsync(NamedPipeServerStream pipe, ChannelReader<byte[]> outgoing, CancellationToken ct)
    {
        try
        {
            await foreach (var message in outgoing.ReadAllAsync(ct))
                await pipe.WriteAsync(message, ct);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException)
        {
            // Disconnected; reading notices it too.
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait();
        _stop.Dispose();
    }
}
