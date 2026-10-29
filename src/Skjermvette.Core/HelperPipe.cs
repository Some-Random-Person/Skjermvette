using System.Text.Json;
using System.Text.Json.Serialization;
using Skjermvette.Core.Models;

namespace Skjermvette.Core;

/// <summary>
/// What the Windows agent's Helper sends the Service over a named pipe, about once a second: one message per sample,
/// as a JSON <see cref="HelperSample"/>: everything in use right now, and the browsers whose extension is connected.
/// The Service answers on the same pipe with <see cref="HelperMessage"/>s, one JSON object per line.
/// </summary>
public static class HelperPipe
{
    public const string Name = "Skjermvette.Agent";

    /// <summary>Tells the Helper it was started by the Service, which keeps one per session, so it needs no mutex.</summary>
    public const string ServiceArgument = "--service";

    /// <summary>Longer messages are refused, so a bad client can't make the Service buffer without limit.</summary>
    public const int MaxMessageBytes = 8192;

    public const int MaxKeys = 32;
    public const int MaxIdentifierLength = 255;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static byte[] Encode(HelperSample sample) => JsonSerializer.SerializeToUtf8Bytes(sample, Json);

    /// <summary>The sample in <paramref name="message"/>, or null if it isn't a valid one.</summary>
    public static HelperSample? Decode(ReadOnlySpan<byte> message)
    {
        HelperSample? sample;
        try
        {
            sample = JsonSerializer.Deserialize<HelperSample>(message, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        var valid = sample is { InUse.Length: <= MaxKeys, Browsers.Length: <= MaxKeys }
            && sample.InUse.All(k => k is not null && Enum.IsDefined(k.Kind) && Valid(k.Identifier))
            && sample.Browsers.All(Valid);
        return valid ? sample : null;
    }

    private static bool Valid(string? identifier) => !string.IsNullOrWhiteSpace(identifier) && identifier.Length <= MaxIdentifierLength;

    /// <summary>One line: the JSON has no line breaks of its own.</summary>
    public static byte[] EncodeMessage(HelperMessage message) => [.. JsonSerializer.SerializeToUtf8Bytes(message, Json), (byte)'\n'];

    /// <summary>The message on <paramref name="line"/>, or null if it isn't a valid one.</summary>
    public static HelperMessage? DecodeMessage(string line)
    {
        HelperMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<HelperMessage>(line, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        var valid = message switch
        {
            { Notice: { } notice, Websites: null } => Enum.IsDefined(notice.Kind),
            { Notice: null, Websites: { Blocked: not null, Allowed: not null } websites } => websites.Blocked.All(b => b?.Domain is not null)
                && websites.Allowed.All(d => d is not null),
            _ => false,
        };
        return valid ? message : null;
    }
}
