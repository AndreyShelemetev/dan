namespace PamyatRyadom.Api.Services.Auth;

public sealed class AuthOptions
{
    public string CookieName { get; set; } = "pamyat_ryadom_auth";
    public int CodeTtlMinutes { get; set; } = 10;
    public int MaxCodeAttempts { get; set; } = 5;
    public int ClientSessionTtlDays { get; set; } = 14;
    public int PrivilegedSessionTtlHours { get; set; } = 12;

    /// <summary>Absolute ceiling on a client/executor session, measured from
    /// <c>AuthSession.CreatedAt</c> and never from the last use. Rotation slides <c>ExpiresAt</c>
    /// forward on every request, so without this cap a session that is used at least once per TTL
    /// window never expires — a stolen cookie that keeps being replayed stays valid forever. 30 days
    /// means an always-open browser re-authenticates roughly monthly while still riding the 14-day
    /// sliding TTL in between, so ordinary use is never interrupted.</summary>
    public int MaxSessionLifetimeDays { get; set; } = 30;

    /// <summary>The same ceiling for an MFA-elevated (<c>AuthSession.IsPrivileged</c>) session, which
    /// carries operator or admin authority and therefore gets a far shorter wall. 24 hours allows at
    /// most one slide of the 12-hour privileged TTL, so a dispatcher/finance/admin account re-logs in
    /// and re-does the MFA step-up at least daily — long enough to cover one working shift signed in
    /// without a break, short enough that a leaked privileged cookie is worth at most a day.</summary>
    public int MaxPrivilegedSessionLifetimeHours { get; set; } = 24;

    /// <summary>Rotate a session's opaque token once more than this fraction of the session's total
    /// TTL has elapsed since it was issued or last rotated (see AuthService.GetCurrentUserAsync).
    /// E.g. 0.25 with a 14-day client session rotates the token roughly every 3.5 days of continued
    /// use — bounding how long a stolen cookie stays valid without ever forcing a re-login.</summary>
    public double SessionRotationThreshold { get; set; } = 0.25;
}
