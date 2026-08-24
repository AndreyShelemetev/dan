using PamyatRyadom.Api.Dtos.Auth;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>What a successful OTP verification hands back to the controller: the DTO the caller sees,
/// plus the raw session token and its expiry — which exist only to be written into the HttpOnly
/// cookie and are deliberately NOT part of <see cref="VerifyOtpResponseDto"/>, so the token can never
/// end up in a JSON body.</summary>
public sealed class VerifiedOtpResult
{
    public required string SessionToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required VerifyOtpResponseDto Response { get; init; }
}
