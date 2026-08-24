using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>TOTP enrollment/verification for privileged roles. The shared secret is encrypted with
/// ASP.NET Core Data Protection before it reaches the database, and recovery codes are stored only as
/// PBKDF2 hashes (<see cref="SecretHasher"/>) — a database dump alone can neither generate a valid
/// code nor replay a recovery code.</summary>
public sealed class MfaService : IMfaService
{
    /// <summary>Shown by the authenticator app above the account name.</summary>
    public const string DefaultIssuer = "Память рядом";

    private const string DataProtectionPurpose = "PamyatRyadom.Auth.MfaSecret.v1";
    private const int SecretBytes = 20;
    private const int TotpStepSeconds = 30;
    private const int TotpDigits = 6;

    /// <summary>Accept the neighbouring time steps so a phone clock off by a few seconds still works.
    /// One step each way costs a factor-of-3 brute-force window, which the login rate limit covers.</summary>
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    /// <summary>No 0/O/1/I — recovery codes get read off a screen and typed by hand.</summary>
    private const string RecoveryAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly IDataProtector _protector;
    private readonly ILogger<MfaService> _logger;

    public MfaService(IDataProtectionProvider dataProtectionProvider, ILogger<MfaService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _logger = logger;
    }

    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretBytes));

    public string GetProvisioningUri(string secret, string account, string? issuer = null)
    {
        var effectiveIssuer = string.IsNullOrWhiteSpace(issuer) ? DefaultIssuer : issuer;
        var label = $"{Uri.EscapeDataString(effectiveIssuer)}:{Uri.EscapeDataString(account)}";

        return $"otpauth://totp/{label}" +
               $"?secret={secret}" +
               $"&issuer={Uri.EscapeDataString(effectiveIssuer)}" +
               "&algorithm=SHA1" +
               $"&digits={TotpDigits}" +
               $"&period={TotpStepSeconds}";
    }

    public bool VerifyCode(string secret, string? code)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32Encoding.ToBytes(secret);
        }
        catch (ArgumentException)
        {
            // A malformed stored secret is a data problem, not a user error — never a successful login.
            _logger.LogError("Stored MFA secret is not valid Base32.");
            return false;
        }

        var totp = new Totp(key, step: TotpStepSeconds, totpSize: TotpDigits);
        return totp.VerifyTotp(trimmed, out _, Window);
    }

    public RecoveryCodeSet GenerateRecoveryCodes(int count = 10)
    {
        var codes = new List<string>(count);
        var hashes = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            var code = GenerateRecoveryCode();
            codes.Add(code);
            hashes.Add(SecretHasher.Hash(code));
        }

        return new RecoveryCodeSet(codes, hashes);
    }

    public string ProtectSecret(string secret) => _protector.Protect(secret);

    public string? UnprotectSecret(string protectedSecret)
    {
        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            // Key ring rotated/lost: the user must re-enroll. Logged without any secret material.
            _logger.LogError("Failed to unprotect a stored MFA secret — re-enrollment is required.");
            return null;
        }
    }

    /// <summary>10 characters as <c>XXXXX-XXXXX</c>, ~50 bits of entropy.</summary>
    private static string GenerateRecoveryCode()
    {
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);

        var builder = new StringBuilder(11);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i == 5)
            {
                builder.Append('-');
            }

            builder.Append(RecoveryAlphabet[bytes[i] % RecoveryAlphabet.Length]);
        }

        return builder.ToString();
    }
}
