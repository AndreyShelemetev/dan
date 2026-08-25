using System.Net;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>
/// The absolute session lifetime wall. Rotation slides <c>expires_at</c> forward on every request, so
/// without this cap a cookie that keeps being replayed never expires. The wall is anchored on
/// <c>auth_sessions.created_at</c>, which rotation never moves.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionAbsoluteLifetimeTests : AuthIntegrationTest
{
    private const string Email = "lifetime@example.com";

    public SessionAbsoluteLifetimeTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task A_session_past_the_wall_is_rejected_and_revoked_even_though_it_has_not_expired()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        // Default wall is 30 days. Kept in constant use ever since: rotated an hour ago, and the
        // sliding expiry is still nearly two weeks out.
        await BackdateSessionAsync(
            factory,
            session.Id,
            createdAgo: TimeSpan.FromDays(31),
            rotatedAgo: TimeSpan.FromHours(1),
            expiresIn: TimeSpan.FromDays(13));

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("unauthorized", response.ErrorCode);

        // Revoked, not merely refused — the row can never be resurrected by a later request.
        Assert.NotNull((await SingleSessionAsync(factory)).RevokedAt);
    }

    [Fact]
    public async Task Killing_a_session_at_the_wall_is_audited_with_its_reason()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        await BackdateSessionAsync(factory, session.Id, createdAgo: TimeSpan.FromDays(31));
        await client.GetAsync(MePath);

        var metadata = await LatestAuditMetadataAsync(factory, SecurityAuditEventTypes.SessionRevoked);
        Assert.NotNull(metadata);
        Assert.Equal("max_lifetime_exceeded", metadata["reason"]?.GetValue<string>());
        Assert.Equal("session", metadata["scope"]?.GetValue<string>());
        Assert.Equal(session.Id, metadata["session_id"]?.GetValue<long>());
        Assert.Equal(720d, metadata["max_lifetime_hours"]?.GetValue<double>());
        Assert.Equal(1, await AuditCountAsync(factory, SecurityAuditEventTypes.SessionRevoked));
    }

    [Fact]
    public async Task Replaying_a_session_that_hit_the_wall_never_brings_it_back()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var token = client.SessionToken!;
        var session = await SingleSessionAsync(factory);

        await BackdateSessionAsync(factory, session.Id, createdAgo: TimeSpan.FromDays(31));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            client.SessionToken = token;
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).Status);
        }
    }

    [Fact]
    public async Task A_session_short_of_the_wall_still_works()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        await BackdateSessionAsync(factory, session.Id, createdAgo: TimeSpan.FromDays(29));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MePath)).Status);
        Assert.Null((await SingleSessionAsync(factory)).RevokedAt);
    }

    [Fact]
    public async Task Rotation_never_slides_the_expiry_past_the_wall()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        // 29 days old: due a rotation, and only one day short of the 30-day wall.
        await BackdateSessionAsync(factory, session.Id, createdAgo: TimeSpan.FromDays(29));

        var response = await client.GetAsync(MePath);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull(response.SetSessionToken);

        var rotated = await SingleSessionAsync(factory);
        Assert.NotNull(rotated.RotatedAt);

        // Would have been now + 14 days without the cap.
        Assert.True(
            rotated.ExpiresAt <= DateTimeOffset.UtcNow.AddDays(1).AddMinutes(1),
            $"expiry {rotated.ExpiresAt:O} slid past the absolute wall");
    }

    [Fact]
    public async Task The_configured_wall_is_honoured()
    {
        var factory = CreateFactory(Settings(
            ("Auth:ClientSessionTtlDays", "1"),
            ("Auth:MaxSessionLifetimeDays", "2")));

        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        // Inside the 1-day sliding TTL, outside the 2-day wall.
        await BackdateSessionAsync(
            factory,
            session.Id,
            createdAgo: TimeSpan.FromDays(3),
            expiresIn: TimeSpan.FromHours(12));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).Status);
        Assert.NotNull((await SingleSessionAsync(factory)).RevokedAt);
    }

    [Fact]
    public async Task A_privileged_session_gets_the_shorter_wall()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        // 25 hours old against the 24-hour privileged wall, and still an hour from expiring.
        await BackdateSessionAsync(
            factory,
            session.Id,
            createdAgo: TimeSpan.FromHours(25),
            expiresIn: TimeSpan.FromHours(1),
            isPrivileged: true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).Status);
        Assert.NotNull((await SingleSessionAsync(factory)).RevokedAt);
    }

    [Fact]
    public async Task A_privileged_session_inside_the_shorter_wall_survives()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        await BackdateSessionAsync(
            factory,
            session.Id,
            createdAgo: TimeSpan.FromHours(20),
            expiresIn: TimeSpan.FromHours(1),
            isPrivileged: true);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MePath)).Status);
    }

    [Fact]
    public async Task A_wall_misconfigured_below_the_ttl_never_kills_a_brand_new_session()
    {
        // The documented invariant: the wall is clamped up to one sliding TTL window, so a cap of
        // zero can only shorten a session, never make login hand back a dead cookie.
        var factory = CreateFactory(Settings(
            ("Auth:MaxSessionLifetimeDays", "0"),
            ("Auth:MaxPrivilegedSessionLifetimeHours", "0")));

        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MePath)).Status);
        Assert.Null((await SingleSessionAsync(factory)).RevokedAt);
    }
}
