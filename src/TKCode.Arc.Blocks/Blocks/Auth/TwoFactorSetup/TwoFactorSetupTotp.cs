using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TKCode.Arc;

/// <summary>
/// Time-based one-time passwords (RFC 6238 with HMAC-SHA1, six digits, 30 second steps) and the Base32 alphabet of
/// RFC 4648 that authenticator apps use for secrets.
/// </summary>
internal static class TwoFactorSetupTotp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public const int Digits = 6;
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>The secret without spaces, dashes and padding, in upper case; null when a character is not Base32.</summary>
    public static string? Normalize(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }
        var chars = new char[secret.Length];
        var length = 0;
        foreach (var c in secret)
        {
            if (c is ' ' or '-' or '=')
            {
                continue;
            }
            var upper = char.ToUpperInvariant(c);
            if (Alphabet.IndexOf(upper) < 0)
            {
                return null;
            }
            chars[length++] = upper;
        }
        return length == 0 ? null : new string(chars, 0, length);
    }

    /// <summary>Decodes a Base32 secret (spaces and padding allowed); null when it is not Base32.</summary>
    public static byte[]? Decode(string? secret)
    {
        if (Normalize(secret) is not { } text)
        {
            return null;
        }
        var bytes = new byte[text.Length * 5 / 8];
        var buffer = 0;
        var bits = 0;
        var index = 0;
        foreach (var c in text)
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
            }
        }
        return bytes;
    }

    /// <summary>Encodes bytes as Base32 without padding.</summary>
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[(bytes.Length * 8 + 4) / 5];
        var buffer = 0;
        var bits = 0;
        var index = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                chars[index++] = Alphabet[(buffer >> bits) & 31];
            }
        }
        if (bits > 0)
        {
            chars[index++] = Alphabet[(buffer << (5 - bits)) & 31];
        }
        return new string(chars, 0, index);
    }

    /// <summary>The six-digit code for <paramref name="key"/> at <paramref name="time"/>.</summary>
    public static string Code(byte[] key, DateTimeOffset time)
    {
        var counter = time.ToUnixTimeSeconds() / (long)Step.TotalSeconds;
        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, message, hash);
        var offset = hash[19] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>True when <paramref name="code"/> is the code of the current step or of the step before or after it (clock drift).</summary>
    public static bool Verify(string? secret, string? code, DateTimeOffset now)
    {
        if (Decode(secret) is not { Length: > 0 } key || code is null || code.Length != Digits)
        {
            return false;
        }
        for (var drift = -1; drift <= 1; drift++)
        {
            if (Code(key, now + Step * drift) == code)
            {
                return true;
            }
        }
        return false;
    }
}
