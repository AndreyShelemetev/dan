using System.Net;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>Token rotation: once a session has been carrying the same secret for
/// <c>Auth:SessionRotationThreshold</c> of its TTL, the next request re-issues it in place. Bounds how
/// long a stolen cookie stays usable without ever forcing a re-login.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionRotationTests : AuthIntegrationTest
{
    private const string Email = "rotation@example.com";

    /// <summary>Past the default threshold (0.25 of the 14-day client TTL = 3.5 days).</summary>
    private static readonly TimeSpan PastThreshold = TimeSpan.FromDays(4);

    /// <summary>Comfortably short of it.</summary>
    private static readonly TimeSpan BeforeThreshold = TimeSpan.FromDays(3);

    public SessionRotationTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Past_the_threshold_a_new_token_is_issued_and_the_old_one_dies()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var oldToken = client.SessionToken!;
        var before = await SingleSessionAsync(factory);

        await BackdateSessionAsync(factory, before.Id, createdAgo: PastThreshold);

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull(response.SetSessionToken);
        Assert.NotEqual(oldToken, response.SetSessionToken);
        Assert.Equal(response.SetSessionToken, client.SessionToken);

        // Same row, new secret: rotation is not a re-login.
        var after = await SingleSessionAsync(factory);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.UserId, after.UserId);
        Assert.NotNull(after.RotatedAt);
        Assert.NotEqual(before.SessionTokenHash, after.SessionTokenHash);
        Assert.True(after.ExpiresAt > before.ExpiresAt, "rotation should slide the expiry forward");
        Assert.Null(after.RevokedAt);

        // A cookie captured before the rotation is now worthless.
        var stale = factory.CreateApiClient();
        stale.SessionToken = oldToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await stale.GetAsync(MePath)).Status);

        // And the rotated cookie keeps working, without rotating again.
        var next = await client.GetAsync(MePath);
        Assert.Equal(HttpStatusCode.OK, next.Status);
        Assert.Null(next.SetSessionToken);
    }

    [Fact]
    public async Task Before_the_threshold_the_token_is_left_alone()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var token = client.SessionToken!;
        var session = await SingleSessionAsync(factory);

        await BackdateSessionAsync(factory, session.Id, createdAgo: BeforeThreshold);

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(response.SetSessionToken);
        Assert.Equal(token, client.SessionToken);
        Assert.Null((await SingleSessionAsync(factory)).RotatedAt);
    }

    [Fact]
    public async Task Rotation_is_measured_from_the_last_rotation_not_from_creation()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        // Long-lived session, but rotated an hour ago: nothing to do yet.
        await BackdateSessionAsync(
            factory,
            session.Id,
            createdAgo: TimeSpan.FromDays(10),
            rotatedAgo: TimeSpan.FromHours(1));

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(response.SetSessionToken);
    }

    [Fact]
    public async Task A_zero_threshold_disables_rotation()
    {
        var factory = CreateFactory(Settings(("Auth:SessionRotationThreshold", "0")));
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        await BackdateSessionAsync(factory, session.Id, createdAgo: TimeSpan.FromDays(13));

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(response.SetSessionToken);
        Assert.Null((await SingleSessionAsync(factory)).RotatedAt);
    }
}
