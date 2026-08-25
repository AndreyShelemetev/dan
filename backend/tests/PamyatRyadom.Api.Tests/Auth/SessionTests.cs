using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary><c>GET /auth/me</c>, <c>POST /auth/logout</c> and <c>POST /auth/logout-all</c>: who the
/// cookie resolves to, and what it takes to make it stop resolving to anyone.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionTests : AuthIntegrationTest
{
    private const string Email = "session@example.com";

    public SessionTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);

        var user = await SingleUserAsync(factory);
        Assert.Equal(user.Id, response.Data?["id"]?.GetValue<long>());
        Assert.Equal(Email, response.Data?["email"]?.GetValue<string>());
        Assert.Equal(UserRoles.Client, response.Data?["role"]?.GetValue<string>());
        Assert.False(response.Data?["mfaEnabled"]?.GetValue<bool>());
    }

    [Fact]
    public async Task Me_without_a_cookie_is_401()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("unauthorized", response.ErrorCode);
        Assert.Null(response.Data);
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Me_with_a_garbage_cookie_is_401(string garbage)
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        // A real session exists — the garbage token must not resolve to it (or to anything else).
        await LoginAsync(factory, client, Email);
        client.SessionToken = garbage;

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("unauthorized", response.ErrorCode);
    }

    [Fact]
    public async Task Me_with_another_users_token_hash_is_401()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        // The stored hash is not a credential: presenting it must not authenticate anyone.
        var session = await SingleSessionAsync(factory);
        client.SessionToken = session.SessionTokenHash;

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
    }

    [Fact]
    public async Task Logout_clears_the_cookie_revokes_the_row_and_kills_the_token()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var token = client.SessionToken!;

        var logout = await client.PostAsync(LogoutPath);

        Assert.Equal(HttpStatusCode.OK, logout.Status);
        Assert.True(logout.SessionCookieCleared);
        Assert.Null(client.SessionToken);

        var session = await SingleSessionAsync(factory);
        Assert.NotNull(session.RevokedAt);

        // Server-side revocation, not just a cleared cookie: the old token is worthless.
        client.SessionToken = token;
        var afterLogout = await client.GetAsync(MePath);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.Status);

        Assert.Equal(1, await AuditCountAsync(factory, SecurityAuditEventTypes.Logout));
    }

    [Fact]
    public async Task Logout_without_a_cookie_still_succeeds()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(LogoutPath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(response.SessionCookieCleared);
    }

    [Fact]
    public async Task Logout_all_revokes_every_session_of_the_user()
    {
        var factory = CreateFactory();
        var browser = factory.CreateApiClient();
        var phone = factory.CreateApiClient();

        await LoginAsync(factory, browser, Email);
        await LoginAsync(factory, phone, Email);

        var phoneToken = phone.SessionToken!;
        Assert.Equal(2, await factory.QueryDbAsync(db => db.AuthSessions.CountAsync(x => x.RevokedAt == null)));

        var response = await browser.PostAsync(LogoutAllPath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(response.SessionCookieCleared);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.AuthSessions.CountAsync(x => x.RevokedAt == null)));

        // The other device is signed out too, without ever having been touched.
        phone.SessionToken = phoneToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync(MePath)).Status);
    }

    [Fact]
    public async Task Logout_all_only_touches_the_calling_users_sessions()
    {
        var factory = CreateFactory();
        var mine = factory.CreateApiClient();
        var theirs = factory.CreateApiClient();

        await LoginAsync(factory, mine, Email);
        await LoginAsync(factory, theirs, "someone.else@example.com");

        await mine.PostAsync(LogoutAllPath);

        Assert.Equal(HttpStatusCode.OK, (await theirs.GetAsync(MePath)).Status);
        Assert.Equal(1, await factory.QueryDbAsync(db => db.AuthSessions.CountAsync(x => x.RevokedAt == null)));
    }

    [Fact]
    public async Task Logout_all_without_a_session_is_401()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(LogoutAllPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("unauthorized", response.ErrorCode);
    }

    [Fact]
    public async Task Blocking_the_account_kills_its_live_session_on_the_next_request()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetUserStatusAsync(factory, UserStatuses.Blocked);

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.NotNull((await SingleSessionAsync(factory)).RevokedAt);
    }

    [Fact]
    public async Task A_privileged_role_gets_the_short_session_ttl_at_login()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, UserRoles.Dispatcher);

        // Second login, now as a dispatcher.
        var second = factory.CreateApiClient();
        await LoginAsync(factory, second, Email);

        var session = await factory.QueryDbAsync(db => db.AuthSessions.OrderByDescending(x => x.Id).FirstAsync());

        // Note what this pins: IsPrivileged is decided by the account's role at login, with no MFA
        // step-up involved. Its effect here is the short (12-hour) TTL, not an authorization gate —
        // RequireRoleFilter never inspects it.
        Assert.True(session.IsPrivileged);
        Assert.InRange(
            session.ExpiresAt,
            DateTimeOffset.UtcNow.AddHours(11),
            DateTimeOffset.UtcNow.AddHours(13));
    }

    [Fact]
    public async Task An_expired_session_is_401()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var session = await SingleSessionAsync(factory);

        await BackdateSessionAsync(factory, session.Id, createdAgo: TimeSpan.FromDays(15), expiresIn: TimeSpan.FromMinutes(-1));

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
    }
}
