using PamyatRyadom.Api.Dtos.Auth;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>The resolved owner of a live session, plus everything the caller needs to keep the cookie
/// in sync after a rotation.</summary>
public sealed class CurrentUserResult
{
    public required AuthUserDto User { get; init; }

    /// <summary>The token the caller should keep using: the freshly rotated one when
    /// <see cref="Rotated"/> is true, otherwise the token that was presented.</summary>
    public required string SessionToken { get; init; }

    /// <summary>True when the session token was just rotated — the controller/filter must re-set the
    /// cookie, or the next request will present a hash that no longer exists.</summary>
    public required bool Rotated { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Whether this session carries the short privileged TTL (see AuthSession.IsPrivileged).</summary>
    public required bool IsPrivilegedSession { get; init; }
}
