namespace PamyatRyadom.Api.Services.Auth;

/// <summary>Freshly generated recovery codes: the raw codes are shown to the user exactly once,
/// only <see cref="Hashes"/> is ever persisted (see MfaSecret.RecoveryCodesHash).</summary>
public sealed record RecoveryCodeSet(IReadOnlyList<string> Codes, IReadOnlyList<string> Hashes);

/// <summary>TOTP primitives (RFC 6238, via Otp.NET) plus at-rest protection of the shared secret.
/// Deliberately stateless — persistence of MfaSecret rows belongs to <see cref="IAuthService"/>.</summary>
public interface IMfaService
{
    /// <summary>A new Base32-encoded TOTP secret (160 bits, the RFC 4226 recommendation).</summary>
    string GenerateSecret();

    /// <summary>otpauth:// URI for authenticator apps. <paramref name="issuer"/> defaults to the
    /// product name.</summary>
    string GetProvisioningUri(string secret, string account, string? issuer = null);

    /// <summary>Validates a 6-digit code with a one-step (±30s) tolerance for clock drift.</summary>
    bool VerifyCode(string secret, string? code);

    RecoveryCodeSet GenerateRecoveryCodes(int count = 10);

    /// <summary>Encrypts a secret for storage (ASP.NET Core Data Protection).</summary>
    string ProtectSecret(string secret);

    /// <summary>Decrypts a stored secret; null when the payload can't be unprotected (e.g. the data
    /// protection key ring was rotated away) rather than throwing into a request pipeline.</summary>
    string? UnprotectSecret(string protectedSecret);
}
