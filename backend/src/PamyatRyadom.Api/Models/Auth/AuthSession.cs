using System.Net;

namespace PamyatRyadom.Api.Models.Auth;

/// <summary>One opaque bearer session. Only the SHA-256 hash of the token is stored — the raw token
/// lives solely in the HttpOnly/Secure/SameSite=Lax cookie handed to the browser. Not a JWT: revocation
/// and rotation both just mean updating this row, no token blacklist needed.</summary>
public sealed class AuthSession
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string SessionTokenHash { get; set; } = string.Empty;
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    /// <summary>When the token was last rotated (re-issued in place, old hash replaced). Null means
    /// the session is still on the token it was issued with.</summary>
    public DateTimeOffset? RotatedAt { get; set; }
    /// <summary>Set once this session has completed an MFA step-up (see mfa/verify). Privileged
    /// sessions carry a short TTL (<c>AuthOptions.PrivilegedSessionTtlHours</c>) instead of the normal
    /// client TTL, and are what <see cref="Services.Auth.RequireRoleFilter"/> requires for roles that
    /// have MFA enrolled.</summary>
    public bool IsPrivileged { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }
}
