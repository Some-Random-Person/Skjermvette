using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenTime.Core.Models;

namespace ScreenTime.Core;

/// <summary>
/// What the Windows agent's Helper sends the Service over a named pipe, about once a second: one message per sample,
/// holding everything in use right now as a JSON array of <see cref="UsageKey"/>. An empty array means nothing is in use.
/// </summary>
public static class HelperPipe
{
    public const string Name = "ScreenTime.Agent";

    /// <summary>Longer messages are refused, so a bad client can't make the Service buffer without limit.</summary>
    public const int MaxMessageBytes = 8192;

    public const int MaxKeys = 32;
    public const int MaxIdentifierLength = 255;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static byte[] Encode(IReadOnlyCollection<UsageKey> inUse) => JsonSerializer.SerializeToUtf8Bytes(inUse, Json);

    /// <summary>The sample in <paramref name="message"/>, or null if it isn't a valid one.</summary>
    public static UsageKey[]? Decode(ReadOnlySpan<byte> message)
    {
        UsageKey[]? keys;
        try
        {
            keys = JsonSerializer.Deserialize<UsageKey[]>(message, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        var valid = keys is { Length: <= MaxKeys } && keys.All(k =>
            k is not null && Enum.IsDefined(k.Kind) && !string.IsNullOrWhiteSpace(k.Identifier) && k.Identifier.Length <= MaxIdentifierLength);
        return valid ? keys : null;
    }
}
