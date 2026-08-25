using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>The session/OTP parameters are security policy documented in CLAUDE.md, and every one of
/// them is a plain default on <see cref="AuthOptions"/> that a stray edit could change without any
/// other test noticing. This pins them.</summary>
public sealed class AuthOptionsTests
{
    [Fact]
    public void The_documented_defaults_are_what_an_unconfigured_deployment_gets()
    {
        var options = new AuthOptions();

        Assert.Equal("pamyat_ryadom_auth", options.CookieName);

        Assert.Equal(10, options.CodeTtlMinutes);
        Assert.Equal(5, options.MaxCodeAttempts);

        Assert.Equal(14, options.ClientSessionTtlDays);
        Assert.Equal(12, options.PrivilegedSessionTtlHours);

        Assert.Equal(30, options.MaxSessionLifetimeDays);
        Assert.Equal(24, options.MaxPrivilegedSessionLifetimeHours);

        Assert.Equal(0.25, options.SessionRotationThreshold);
    }

    [Fact]
    public void Every_absolute_ceiling_leaves_room_for_at_least_one_sliding_window()
    {
        var options = new AuthOptions();

        Assert.True(
            TimeSpan.FromDays(options.MaxSessionLifetimeDays) > TimeSpan.FromDays(options.ClientSessionTtlDays),
            "a client session would be cut short before its sliding TTL ever elapsed");

        Assert.True(
            TimeSpan.FromHours(options.MaxPrivilegedSessionLifetimeHours) > TimeSpan.FromHours(options.PrivilegedSessionTtlHours),
            "a privileged session would be cut short before its sliding TTL ever elapsed");
    }
}
