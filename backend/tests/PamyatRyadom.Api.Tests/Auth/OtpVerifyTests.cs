using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary><c>POST /api/v1/auth/otp/verify</c>, happy path: first login registers the account and
/// everything that legally has to accompany it, later logins reuse it.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpVerifyTests : AuthIntegrationTest
{
    private const string Email = "first.login@example.com";

    public OtpVerifyTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task First_login_creates_the_user_and_a_verified_identity()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(response.Data?["isNewUser"]?.GetValue<bool>());
        Assert.Equal(UserRoles.Client, response.Data?["user"]?["role"]?.GetValue<string>());
        Assert.Equal(UserStatuses.Active, response.Data?["user"]?["status"]?.GetValue<string>());
        Assert.Equal(Email, response.Data?["user"]?["email"]?.GetValue<string>());
        Assert.False(response.Data?["user"]?["mfaEnabled"]?.GetValue<bool>());

        var user = await SingleUserAsync(factory);
        Assert.Equal(UserRoles.Client, user.Role);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal("ru", user.Locale);
        Assert.NotNull(user.LastLoginAt);

        var identity = await factory.QueryDbAsync(db => db.AuthIdentities.SingleAsync());
        Assert.Equal(user.Id, identity.UserId);
        Assert.Equal(AuthProviders.Email, identity.Provider);
        Assert.Equal(Email, identity.Email);
        Assert.Null(identity.Phone);
        Assert.True(identity.IsVerified);
        Assert.NotNull(identity.LastLoginAt);
    }

    [Fact]
    public async Task First_login_sets_a_hardened_session_cookie_and_stores_only_its_hash()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await LoginAsync(factory, client, Email);

        var cookie = response.AuthCookie;
        Assert.NotNull(cookie);
        Assert.True(cookie!.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/", cookie.Path.Value);
        Assert.NotNull(cookie.Expires);

        var token = response.SetSessionToken!;
        Assert.NotEmpty(token);

        // The token itself must never appear in a response body.
        Assert.DoesNotContain(token, response.Raw);

        var session = await SingleSessionAsync(factory);
        Assert.Null(session.RevokedAt);
        Assert.Null(session.RotatedAt);
        Assert.False(session.IsPrivileged);
        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.NotEqual(token, session.SessionTokenHash);
        Assert.Matches("^[0-9a-f]{64}$", session.SessionTokenHash);
    }

    [Fact]
    public async Task First_login_consumes_the_code()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        var otp = await factory.QueryDbAsync(db => db.OtpCodes.SingleAsync());
        Assert.NotNull(otp.ConsumedAt);
        Assert.Equal(0, otp.AttemptCount);
    }

    [Fact]
    public async Task First_login_writes_the_personal_data_consent_and_the_legal_acceptance()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        var user = await SingleUserAsync(factory);

        var consent = await factory.QueryDbAsync(db => db.ConsentLogs.SingleAsync());
        Assert.Equal(user.Id, consent.UserId);
        Assert.Equal(ConsentTypes.PersonalData, consent.ConsentType);
        // The published privacy-policy version, not the old placeholder: what a person consented
        // to has to identify the text that was actually in force.
        Assert.Equal("1.0", consent.DocumentVersion);
        Assert.Equal(Email, consent.Email);
        Assert.Null(consent.Phone);
        Assert.Null(consent.RevokedAt);
        Assert.NotEqual(default, consent.AcceptedAt);

        // Document acceptance is tracked against the versioned rows, not in the consent log.
        var documents = await factory.QueryDbAsync(db => db.LegalDocuments.ToListAsync());
        Assert.All(documents, d =>
        {
            Assert.Equal("1.0", d.Version);
            Assert.Equal("ru", d.Locale);
            Assert.Equal(LegalDocumentStatuses.Published, d.Status);
        });

        // One acceptance per required instrument: the policy and the consent are separate under
        // 152-ФЗ, so a single combined row would lose which was shown.
        var acceptances = await factory.QueryDbAsync(db => db.LegalAcceptances.ToListAsync());
        Assert.Equal(2, acceptances.Count);
        Assert.All(acceptances, a =>
        {
            Assert.Equal(user.Id, a.UserId);
            Assert.Null(a.RevokedAt);
            Assert.Contains(a.DocumentId, documents.Select(d => d.Id));
        });
    }

    [Fact]
    public async Task First_login_is_audited_as_a_new_user()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        var metadata = await LatestAuditMetadataAsync(factory, SecurityAuditEventTypes.LoginSuccess);
        Assert.NotNull(metadata);
        Assert.True(metadata["is_new_user"]?.GetValue<bool>());
        Assert.Equal(AuthProviders.Email, metadata["provider"]?.GetValue<string>());
        Assert.False(metadata["privileged_session"]?.GetValue<bool>());

        Assert.Equal(1, await AuditCountAsync(factory, SecurityAuditEventTypes.OtpRequested));
        Assert.Equal(1, await AuditCountAsync(factory, SecurityAuditEventTypes.LoginSuccess));
    }

    [Fact]
    public async Task Second_login_reuses_the_account_and_writes_no_second_consent()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        var code = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.False(response.Data?["isNewUser"]?.GetValue<bool>());

        Assert.Equal(1, await factory.QueryDbAsync(db => db.Users.CountAsync()));
        Assert.Equal(1, await factory.QueryDbAsync(db => db.AuthIdentities.CountAsync()));
        // Still exactly what registration wrote: one personal-data consent, one acceptance per
        // required instrument. Signing in again is not a new consent event.
        Assert.Equal(1, await factory.QueryDbAsync(db => db.ConsentLogs.CountAsync()));
        Assert.Equal(2, await factory.QueryDbAsync(db => db.LegalAcceptances.CountAsync()));
        Assert.Equal(3, await factory.QueryDbAsync(db => db.LegalDocuments.CountAsync()));

        // A second login is a second session; the first one is left alone.
        Assert.Equal(2, await factory.QueryDbAsync(db => db.AuthSessions.CountAsync(x => x.RevokedAt == null)));
    }

    [Fact]
    public async Task A_differently_cased_destination_resolves_to_the_same_account()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        var code = await RequestCodeAsync(factory, client, "  FIRST.Login@Example.com ");
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify("FIRST.Login@EXAMPLE.com", code));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.False(response.Data?["isNewUser"]?.GetValue<bool>());
        Assert.Equal(1, await factory.QueryDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task A_blocked_account_cannot_log_in()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetUserStatusAsync(factory, UserStatuses.Blocked);

        var code = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("forbidden", response.ErrorCode);
        Assert.Null(response.SetSessionToken);
    }
}
