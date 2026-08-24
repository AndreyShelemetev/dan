using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>Shared PBKDF2-SHA256 hashing for low-entropy secrets: OTP codes and MFA recovery codes.
/// Session tokens do NOT use this — they're already 256 bits of random entropy, so a plain SHA-256
/// hash is enough (PBKDF2's slow-hash property only matters for guessable secrets like a 6-digit
/// code, where it makes an offline brute-force of a stolen hash impractically slow).</summary>
internal static class SecretHasher
{
    private const int Iterations = 100_000;
    private const string Algorithm = "pbkdf2-sha256";

    public static string Hash(string value)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(value), salt, Iterations, HashAlgorithmName.SHA256, 32);
        return string.Join(
            ':',
            Algorithm,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Base64UrlEncode(salt),
            Base64UrlEncode(hash));
    }

    public static bool Verify(string storedHash, string value)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 4 ||
            parts[0] != Algorithm ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        var salt = Base64UrlDecode(parts[2]);
        var expected = Base64UrlDecode(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(value), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Plain SHA-256 hex digest — for already-high-entropy values (session tokens).</summary>
    public static string HashHighEntropy(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GenerateOpaqueToken(int byteLength = 32) =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(byteLength));

    public static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
