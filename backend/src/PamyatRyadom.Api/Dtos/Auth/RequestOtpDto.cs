using System.ComponentModel.DataAnnotations;

namespace PamyatRyadom.Api.Dtos.Auth;

public sealed class RequestOtpDto
{
    /// <summary>Email address or phone number, depending on <see cref="Channel"/>.</summary>
    [Required]
    public string Destination { get; init; } = string.Empty;

    /// <summary>"email" or "sms" — see Models.Auth.OtpChannels.</summary>
    [Required]
    public string Channel { get; init; } = "email";

    /// <summary>"login" or "verify_email" — see Models.Auth.OtpPurposes. Defaults to "login".</summary>
    public string? Purpose { get; init; }
}
