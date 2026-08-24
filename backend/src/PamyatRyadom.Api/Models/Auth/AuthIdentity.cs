namespace PamyatRyadom.Api.Models.Auth;

/// <summary>A login method attached to a <see cref="User"/> (email or phone OTP in this MVP).
/// Deliberately separate from <see cref="User"/> — account/role/status is not the same concept as
/// "how this person proves who they are". No password field: this MVP is OTP-only.</summary>
public sealed class AuthIdentity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Provider { get; set; } = AuthProviders.Email;
    public string? ProviderUserId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public User? User { get; set; }
}

public static class AuthProviders
{
    public const string Email = "email";
    public const string Phone = "phone";

    public static readonly IReadOnlyCollection<string> All = new[] { Email, Phone };
}
