using System.Buffers.Binary;
using System.IO.Pipes;

namespace Skjermvette.Agent.Helper;

/// <summary>
/// The browser extension's native messaging host. The browser starts the Helper's program with the extension's origin as
/// the first argument and writes the extension's messages to its standard input, each a 4-byte length followed by JSON.
/// They're passed on to the Helper running in this session over its <see cref="BrowserPipe"/>. It ends when the browser
/// closes the connection or the Helper is gone; the extension then connects again.
/// </summary>
public static class BrowserHost
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>True when the browser started this program as the extension's native messaging host.</summary>
    public static bool StartedByBrowser(string[] args) =>
        args.FirstOrDefault()?.StartsWith("chrome-extension://", StringComparison.Ordinal) == true;

    /// <returns>0 when the browser closed the connection, 1 on anything else.</returns>
    public static async Task<int> RunAsync(Stream input, string pipeName)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds);
            var header = new byte[4];
            while (true)
            {
                var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
                if (read == 0)
                    return 0;
                // The length is in the computer's own byte order, which on Windows is little-endian.
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (read < header.Length || length is <= 0 or > BrowserPipe.MaxMessageBytes)
                    return 1;

                var message = new byte[length];
                await input.ReadExactlyAsync(message);
                await pipe.WriteAsync(message);
            }
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // Standard output belongs to the browser; it logs standard error.
            await Console.Error.WriteLineAsync($"Not connected to the Helper: {e.Message}");
            return 1;
        }
    }
}
