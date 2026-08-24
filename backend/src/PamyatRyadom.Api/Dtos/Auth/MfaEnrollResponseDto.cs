namespace PamyatRyadom.Api.Dtos.Auth;

public sealed class MfaEnrollResponseDto
{
    /// <summary>otpauth:// URI — render as a QR code in the frontend.</summary>
    public required string ProvisioningUri { get; init; }

    /// <summary>Base32 secret, for manual entry when a QR scan isn't possible.</summary>
    public required string Secret { get; init; }

    /// <summary>One-time recovery codes, shown to the user exactly once. Only their hashes are
    /// persisted — this response is the only chance to see them in the clear.</summary>
    public required IReadOnlyList<string> RecoveryCodes { get; init; }
}
