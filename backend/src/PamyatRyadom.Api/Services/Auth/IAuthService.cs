using PamyatRyadom.Api.Dtos.Auth;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>The Identity module's whole write surface. Controllers never touch AppDbContext directly.</summary>
public interface IAuthService
{
    /// <summary>Issues a one-time code and delivers it over <paramref name="channel"/>. The result is
    /// identical whether or not an account exists for <paramref name="destination"/> — enumeration of
    /// registered users through this endpoint must not be possible.</summary>
    Task<AuthServiceResult<AuthMessageDto>> RequestOtpAsync(
        string? destination,
        string? channel,
        string? purpose,
        AuthRequestContext context,
        CancellationToken ct = default);

    /// <summary>Consumes a one-time code and logs the user in, creating the account (plus its
    /// registration consent records) on first sight of the destination.</summary>
    Task<AuthServiceResult<VerifiedOtpResult>> VerifyOtpAsync(
        string? destination,
        string? channel,
        string? purpose,
        string? code,
        bool acceptedLegal,
        AuthRequestContext context,
        CancellationToken ct = default);

    /// <summary>Resolves the owner of a live session, rotating the opaque token when enough of the
    /// session's TTL has elapsed. Returns null for a missing/expired/revoked session or a non-active
    /// user. A rotation is reported through <see cref="CurrentUserResult.Rotated"/> — the caller MUST
    /// re-set the cookie in that case.</summary>
    Task<CurrentUserResult?> GetCurrentUserAsync(string? sessionToken, CancellationToken ct = default);

    /// <summary>Revokes the presented session. Idempotent: an unknown or already-revoked token still
    /// succeeds, so a stale cookie can always be cleared.</summary>
    Task<AuthServiceResult<AuthMessageDto>> LogoutAsync(
        string? sessionToken,
        AuthRequestContext context,
        CancellationToken ct = default);

    /// <summary>Revokes every live session of a user — the "sign out everywhere" button, and the
    /// remediation step after a suspected token leak.</summary>
    Task<AuthServiceResult<AuthMessageDto>> RevokeAllSessionsAsync(
        long userId,
        AuthRequestContext context,
        CancellationToken ct = default);

    /// <summary>Starts TOTP enrollment: stores a pending (not yet enabled) secret and returns the
    /// provisioning URI, the Base32 secret and the recovery codes — the only time the raw codes exist.</summary>
    Task<AuthServiceResult<MfaEnrollResponseDto>> EnrollMfaAsync(
        long userId,
        AuthRequestContext context,
        CancellationToken ct = default);

    /// <summary>Confirms a TOTP code: finishes a pending enrollment the first time, and elevates the
    /// presented session to privileged (see AuthSession.IsPrivileged) every time.</summary>
    Task<AuthServiceResult<AuthMessageDto>> ConfirmMfaAsync(
        long userId,
        string? code,
        string? sessionToken,
        AuthRequestContext context,
        CancellationToken ct = default);
}
