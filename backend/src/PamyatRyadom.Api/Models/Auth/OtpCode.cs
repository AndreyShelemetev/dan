using System.Net;

namespace PamyatRyadom.Api.Models.Auth;

/// <summary>A one-time login/verification code. Only <see cref="CodeHash"/> (PBKDF2-SHA256) is ever
/// stored — the raw code is emailed/texted and never persisted.</summary>
public sealed class OtpCode
{
    public long Id { get; set; }
    public string Channel { get; set; } = OtpChannels.Email;
    /// <summary>The email address or phone number the code was sent to (matches
    /// <see cref="AuthIdentity.Email"/>/<see cref="AuthIdentity.Phone"/>, not a user id — this table
    /// is written before any User/AuthIdentity necessarily exists).</summary>
    public string Destination { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string Purpose { get; set; } = OtpPurposes.Login;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public int AttemptCount { get; set; }
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class OtpChannels
{
    public const string Email = "email";
    public const string Sms = "sms";

    public static readonly IReadOnlyCollection<string> All = new[] { Email, Sms };
}

public static class OtpPurposes
{
    public const string Login = "login";
    public const string VerifyEmail = "verify_email";

    public static readonly IReadOnlyCollection<string> All = new[] { Login, VerifyEmail };
}
