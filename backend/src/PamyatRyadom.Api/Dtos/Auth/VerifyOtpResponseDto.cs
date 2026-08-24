namespace PamyatRyadom.Api.Dtos.Auth;

public sealed class VerifyOtpResponseDto
{
    public required AuthUserDto User { get; init; }
    public required bool IsNewUser { get; init; }
}
