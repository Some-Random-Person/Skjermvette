using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Skjermvette.Server.Tests;

/// <summary>What an authenticator app shows (RFC 6238: HMAC-SHA1, 30-second steps, 6 digits).</summary>
public static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The code for <paramref name="key"/> (Base32, as shown to the parent) at <paramref name="time"/> (default: now).</summary>
    public static string Code(string key, DateTimeOffset? time = null)
    {
        var counter = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, (time ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30);
        var hash = HMACSHA1.HashData(Base32Decode(key), counter);
        var offset = hash[^1] & 0x0f;
        var value = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7fffffff;
        return (value % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string key)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in key.ToUpperInvariant().Where(c => c is not (' ' or '=')))
        {
            buffer = (buffer << 5) | Base32Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }
        return [.. bytes];
    }
}
