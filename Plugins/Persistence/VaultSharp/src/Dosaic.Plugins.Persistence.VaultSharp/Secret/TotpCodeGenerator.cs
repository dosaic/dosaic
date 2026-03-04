using System.Globalization;
using System.Security.Cryptography;

namespace Dosaic.Plugins.Persistence.VaultSharp.Secret;

/// <summary>
/// Generates TOTP codes locally using RFC 6238 (HMAC-SHA1 + Unix timestamp).
/// </summary>
public static class TotpCodeGenerator
{
    /// <summary>
    /// Generates a TOTP code from the given Base32-encoded key.
    /// </summary>
    public static string Generate(string base32Key, int periodInSeconds = 30)
    {
        var keyBytes = DecodeBase32(base32Key.Replace(" ", "").ToUpperInvariant());
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / periodInSeconds;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(keyBytes);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0F;
        var code = ((hash[offset] & 0x7F) << 24
                   | (hash[offset + 1] & 0xFF) << 16
                   | (hash[offset + 2] & 0xFF) << 8
                   | (hash[offset + 3] & 0xFF)) % 1_000_000;

        return code.ToString("D6", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the remaining seconds and the UTC expiry for the current TOTP period.
    /// </summary>
    public static (int RemainingSeconds, DateTime ValidUntilUtc) GetPeriodInfo(int periodInSeconds = 30)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = (int)(now.ToUnixTimeSeconds() % periodInSeconds);
        var remaining = periodInSeconds - elapsed;
        var validUntil = now.UtcDateTime.AddSeconds(remaining);
        validUntil = new DateTime(validUntil.Year, validUntil.Month, validUntil.Day,
            validUntil.Hour, validUntil.Minute, validUntil.Second, DateTimeKind.Utc);
        return (remaining, validUntil);
    }

    private static byte[] DecodeBase32(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var accumulator = 0;
        var output = new List<byte>();

        foreach (var c in base32)
        {
            if (c == '=') break;
            var index = alphabet.IndexOf(c);
            if (index < 0)
                throw new ArgumentException($"Invalid Base32 character: {c}", nameof(base32));
            accumulator = (accumulator << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)(accumulator >> bits));
                accumulator &= (1 << bits) - 1;
            }
        }

        return [.. output];
    }
}
