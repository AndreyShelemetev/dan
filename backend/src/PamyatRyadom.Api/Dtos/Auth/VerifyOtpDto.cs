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

    /// <summary>
    /// Whether the person ticked the consent box on the sign-in form.
    ///
    /// Checked server-side, and only when this verification would create an account: a UI-only
    /// gate is no gate at all, since the endpoint accepts a direct request. Returning users are
    /// unaffected — they consented when their account was created, and that record stands.
    /// </summary>
    public bool AcceptedLegal { get; init; }
}
