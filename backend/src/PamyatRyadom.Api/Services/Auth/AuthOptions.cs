namespace PamyatRyadom.Api.Services.Auth;

public sealed class AuthOptions
{
    public string CookieName { get; set; } = "pamyat_ryadom_auth";
    public int CodeTtlMinutes { get; set; } = 10;
    public int MaxCodeAttempts { get; set; } = 5;
    public int ClientSessionTtlDays { get; set; } = 14;
    public int PrivilegedSessionTtlHours { get; set; } = 12;

    /// <summary>Rotate a session's opaque token once more than this fraction of the session's total
    /// TTL has elapsed since it was issued or last rotated (see AuthService.GetCurrentUserAsync).
    /// E.g. 0.25 with a 14-day client session rotates the token roughly every 3.5 days of continued
    /// use — bounding how long a stolen cookie stays valid without ever forcing a re-login.</summary>
    public double SessionRotationThreshold { get; set; } = 0.25;
}
