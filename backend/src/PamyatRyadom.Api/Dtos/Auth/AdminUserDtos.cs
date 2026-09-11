namespace PamyatRyadom.Api.Dtos.Auth;

/// <summary>A user as the admin surface sees it — includes the email an admin needs to identify an
/// account, unlike <see cref="AuthUserDto"/>'s "who am I" shape which comes from a session, not a
/// lookup.</summary>
public sealed class AdminUserDto
{
    public required long Id { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public required bool MfaEnabled { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Creates a staff or executor account outside self-registration. The account signs in
/// through the same OTP flow as everyone else once created — this only reserves the email and the
/// role, it never issues a session or a password.</summary>
public sealed class CreateAdminUserDto
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
}

public sealed class ChangeUserRoleDto
{
    public string Role { get; init; } = string.Empty;
}
