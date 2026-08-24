using System.ComponentModel.DataAnnotations;

namespace PamyatRyadom.Api.Dtos.Auth;

public sealed class MfaVerifyDto
{
    [Required]
    public string Code { get; init; } = string.Empty;
}
