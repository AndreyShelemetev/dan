namespace PamyatRyadom.Api.Dtos.Auth;

/// <summary>Public shape of a User — controllers never return the EF entity directly.</summary>
public sealed class AuthUserDto
{
    public required long Id { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public required bool MfaEnabled { get; init; }
}
