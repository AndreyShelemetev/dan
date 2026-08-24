using System.Net;
using System.Text.Json;

namespace PamyatRyadom.Api.Models.Auth;

/// <summary>Append-only security event trail. Never updated after insert — there is deliberately no
/// UpdatedAt column.</summary>
public sealed class SecurityAuditLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    /// <summary>The role the acting user held at the time of the event (snapshot — a later role
    /// change must not rewrite history).</summary>
    public string? ActorRole { get; set; }
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public JsonDocument? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}

/// <summary>Starter list — extend as new flows land. Adding a value here requires a matching CHECK
/// constraint update (see SecurityAuditLogConfiguration) since Postgres enforces this list, not just
/// C#.</summary>
public static class SecurityAuditEventTypes
{
    public const string LoginSuccess = "login_success";
    public const string LoginFailed = "login_failed";
    public const string Logout = "logout";
    public const string OtpRequested = "otp_requested";
    public const string SessionRevoked = "session_revoked";
    public const string RoleChanged = "role_changed";
    public const string MfaEnrolled = "mfa_enrolled";
    public const string MfaVerified = "mfa_verified";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        LoginSuccess, LoginFailed, Logout, OtpRequested, SessionRevoked, RoleChanged, MfaEnrolled, MfaVerified
    };
}
