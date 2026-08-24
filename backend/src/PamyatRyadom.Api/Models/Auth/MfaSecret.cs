using System.Text.Json;

namespace PamyatRyadom.Api.Models.Auth;

/// <summary>A TOTP secret for one user (at most one per user). <see cref="EnabledAt"/> is null while
/// enrollment is pending confirmation (the user has requested a secret but not yet proven they can
/// generate a valid code) — <see cref="Services.Auth.RequireRoleFilter"/> treats null as "not
/// enrolled" and allows privileged-role logins through with an mfa_setup_required flag rather than
/// hard-blocking, so the very first admin account is never locked out before MFA exists.</summary>
public sealed class MfaSecret
{
    public long Id { get; set; }
    public long UserId { get; set; }
    /// <summary>The TOTP secret, encrypted at rest via ASP.NET Core Data Protection
    /// (see MfaService) — never stored or logged in plaintext.</summary>
    public string SecretEncrypted { get; set; } = string.Empty;
    public DateTimeOffset? EnabledAt { get; set; }
    /// <summary>Hashes (PBKDF2-SHA256) of the one-time recovery codes, as a JSON array. The raw codes
    /// are shown to the user exactly once, at enrollment, and never persisted.</summary>
    public JsonDocument? RecoveryCodesHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }
}
