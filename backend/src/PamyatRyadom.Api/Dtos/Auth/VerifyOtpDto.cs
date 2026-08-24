using System.ComponentModel.DataAnnotations;

namespace PamyatRyadom.Api.Dtos.Auth;

public sealed class VerifyOtpDto
{
    [Required]
    public string Destination { get; init; } = string.Empty;

    [Required]
    public string Channel { get; init; } = "email";

    public string? Purpose { get; init; }

    [Required]
    public string Code { get; init; } = string.Empty;
}
