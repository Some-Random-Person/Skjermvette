using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Channels;
using Skjermvette.Core;
using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Helper;

/// <summary>
/// Sends samples to the Service over its pipe (<see cref="HelperPipe"/>), connecting again whenever the connection is lost,
/// and receives the Service's notices and the websites to block. Only the latest sample waits to be sent; older ones are dropped.
/// </summary>
/// <param name="onWebsites">Called with the websites the browser extension should block, whenever the Service sends them.</param>
public sealed class PipeSender : IDisposable
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    /// <summary>Exactly what the Service grants the user (<c>HelperPipeServer.ClientRights</c>), so the connection isn't refused.</summary>
    private const PipeAccessRights Rights = PipeAccessRights.ReadData | PipeAccessRights.WriteData | PipeAccessRights.ReadAttributes | PipeAccessRights.Synchronize;

    private readonly Channel<UsageKey[]> _samples =
        Channel.CreateBounded<UsageKey[]>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<HelperNotice> _notices =
        Channel.CreateBounded<HelperNotice>(new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    private readonly Action<WebsiteBlocks> _onWebsites;

    public PipeSender(string pipeName = HelperPipe.Name, Action<WebsiteBlocks>? onWebsites = null)
    {
        _onWebsites = onWebsites ?? (_ => { });
        _loop = Task.Run(() => RunAsync(pipeName, _stop.Token));
    }

    public void Send(UsageKey[] inUse) => _samples.Writer.TryWrite(inUse);

    /// <summary>The next notice from the Service, if one has arrived.</summary>
    public bool TryReceive(out HelperNotice notice) => _notices.Reader.TryRead(out notice!);

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
                    using var connected = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var receiving = ReceiveAsync(pipe, connected.Token);
                    try
                    {
                        await foreach (var inUse in _samples.Reader.ReadAllAsync(ct))
                            await pipe.WriteAsync(HelperPipe.Encode(inUse), ct);
                    }
                    finally
                    {
                        await connected.CancelAsync();
                        await receiving;
                    }
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

    private async Task ReceiveAsync(NamedPipeClientStream pipe, CancellationToken ct)
    {
        try
        {
            // The pipe is read as bytes (the user may not switch it to message mode), so notices are one per line.
            using var reader = new StreamReader(pipe, leaveOpen: true);
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                switch (HelperPipe.DecodeMessage(line))
                {
                    case { Notice: { } notice }:
                        _notices.Writer.TryWrite(notice);
                        break;
                    case { Websites: { } websites }:
                        _onWebsites(websites);
                        break;
                }
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException)
        {
            // Disconnected; the sending loop connects again.
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait();
        _stop.Dispose();
    }
}
