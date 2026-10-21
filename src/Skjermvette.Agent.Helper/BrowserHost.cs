using System.Buffers.Binary;
using System.IO.Pipes;

namespace Skjermvette.Agent.Helper;

/// <summary>
/// The browser extension's native messaging host. The browser starts the Helper's program with the extension's origin as
/// the first argument and writes the extension's messages to its standard input, each a 4-byte length followed by JSON.
/// They're passed on to the Helper running in this session over its <see cref="BrowserPipe"/>, and the Helper's messages
/// (the websites to block) are passed back to the extension on standard output, framed the same way. It ends when the
/// browser closes the connection or the Helper is gone; the extension then connects again.
/// </summary>
public static class BrowserHost
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>True when the browser started this program as the extension's native messaging host.</summary>
    public static bool StartedByBrowser(string[] args) =>
        args.FirstOrDefault()?.StartsWith("chrome-extension://", StringComparison.Ordinal) == true;

    /// <returns>0 when the browser closed the connection, 1 on anything else.</returns>
    public static async Task<int> RunAsync(Stream input, Stream output, string pipeName)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds);
            pipe.ReadMode = PipeTransmissionMode.Message;
            return await await Task.WhenAny(ToHelperAsync(input, pipe), ToBrowserAsync(pipe, output));
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // Standard output belongs to the browser; it logs standard error.
            await Console.Error.WriteLineAsync($"Not passed on to the Helper: {e.GetType().Name}: {e.Message}");
            return 1;
        }
    }

    private static async Task<int> ToHelperAsync(Stream input, Stream pipe)
    {
        var header = new byte[4];
        while (true)
        {
            var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            if (read == 0)
                return 0;
            // The length is in the computer's own byte order, which on Windows is little-endian.
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (read < header.Length || length is <= 0 or > BrowserPipe.MaxMessageBytes)
            {
                await Console.Error.WriteLineAsync($"Not a message from the extension: {read} bytes of header, length {length}.");
                return 1;
            }

            var message = new byte[length];
            await input.ReadExactlyAsync(message);
            await pipe.WriteAsync(message);
        }
    }

    private static async Task<int> ToBrowserAsync(PipeStream pipe, Stream output)
    {
        var buffer = new byte[BrowserPipe.MaxWebsitesBytes];
        var header = new byte[4];
        while (true)
        {
            var length = 0;
            do
            {
                var read = length < buffer.Length ? await pipe.ReadAsync(buffer.AsMemory(length)) : 0;
                if (read == 0)
                {
                    await Console.Error.WriteLineAsync(length < buffer.Length ? "The Helper closed the connection." : "Too long a message from the Helper.");
                    return 1;
                }
                length += read;
            }
            while (!pipe.IsMessageComplete);

            BinaryPrimitives.WriteInt32LittleEndian(header, length);
            await output.WriteAsync(header);
            await output.WriteAsync(buffer.AsMemory(0, length));
            await output.FlushAsync();
        }
    }
}
