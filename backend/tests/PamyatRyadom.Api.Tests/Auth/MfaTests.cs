using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary><c>POST /auth/mfa/enroll</c> and <c>POST /auth/mfa/verify</c>: what enrollment hands back
/// once and never again, what the database is allowed to keep, and which codes are accepted.</summary>
[Collection(PostgresCollection.Name)]
public sealed class MfaTests : AuthIntegrationTest
{
    private const string Email = "mfa@example.com";

    /// <summary>The recovery-code alphabet, minus the characters that get misread off a screen
    /// (0/O/1/I).</summary>
    private static readonly Regex RecoveryCodeShape = new("^[A-HJ-NP-Z2-9]{5}-[A-HJ-NP-Z2-9]{5}$");

    public MfaTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Enroll_returns_a_provisioning_uri_a_secret_and_recovery_codes()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var response = await client.PostAsync(MfaEnrollPath);

        Assert.Equal(HttpStatusCode.OK, response.Status);

        var secret = response.Data?["secret"]?.GetValue<string>();
        Assert.NotNull(secret);
        Assert.Matches("^[A-Z2-7]+$", secret!);

        var uri = response.Data?["provisioningUri"]?.GetValue<string>();
        Assert.NotNull(uri);
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains($"secret={secret}", uri);
        Assert.Contains("algorithm=SHA1", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
        Assert.Contains(Uri.EscapeDataString(Email), uri);

        var codes = response.Data?["recoveryCodes"]?.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        Assert.NotNull(codes);
        Assert.Equal(10, codes!.Length);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches(RecoveryCodeShape, code));
    }

    [Fact]
    public async Task Enroll_stores_the_secret_encrypted_and_still_pending()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var response = await client.PostAsync(MfaEnrollPath);

        var secret = response.Data!["secret"]!.GetValue<string>();
        var codes = response.Data["recoveryCodes"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

        var stored = await factory.QueryDbAsync(db => db.MfaSecrets.SingleAsync());

        Assert.Null(stored.EnabledAt);
        Assert.NotEqual(secret, stored.SecretEncrypted);
        Assert.DoesNotContain(secret, stored.SecretEncrypted);

        var hashes = stored.RecoveryCodesHash!.RootElement;
        Assert.Equal(10, hashes.GetArrayLength());

        var raw = hashes.GetRawText();
        Assert.All(codes, code => Assert.DoesNotContain(code, raw));
    }

    [Fact]
    public async Task A_correct_code_completes_enrollment()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var secret = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();

        var response = await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(secret) });

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull((await factory.QueryDbAsync(db => db.MfaSecrets.SingleAsync())).EnabledAt);

        var me = await client.GetAsync(MePath);
        Assert.True(me.Data?["mfaEnabled"]?.GetValue<bool>());

        Assert.Equal(1, await AuditCountAsync(factory, SecurityAuditEventTypes.MfaEnrolled));
        Assert.Equal(1, await AuditCountAsync(factory, SecurityAuditEventTypes.MfaVerified));
    }

    [Fact]
    public async Task A_wrong_code_does_not_complete_enrollment()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var secret = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();

        var response = await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Wrong(secret) });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("mfa_invalid_code", response.ErrorCode);
        Assert.Null((await factory.QueryDbAsync(db => db.MfaSecrets.SingleAsync())).EnabledAt);

        var metadata = await LatestAuditMetadataAsync(factory, SecurityAuditEventTypes.LoginFailed);
        Assert.Equal("mfa_invalid_code", metadata?["reason"]?.GetValue<string>());

        var me = await client.GetAsync(MePath);
        Assert.False(me.Data?["mfaEnabled"]?.GetValue<bool>());
    }

    [Fact]
    public async Task A_code_from_someone_elses_secret_is_rejected()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await client.PostAsync(MfaEnrollPath);

        var foreignSecret = OtpNet.Base32Encoding.ToString(OtpNet.KeyGeneration.GenerateRandomKey(20));
        var response = await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(foreignSecret) });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("mfa_invalid_code", response.ErrorCode);
    }

    [Fact]
    public async Task Verifying_before_enrolling_is_rejected()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var response = await client.PostAsync(MfaVerifyPath, new { code = "123456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("mfa_not_enrolled", response.ErrorCode);
    }

    [Fact]
    public async Task A_pending_enrollment_can_be_restarted_and_the_old_secret_stops_working()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        var first = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();
        var second = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();

        Assert.NotEqual(first, second);
        Assert.Equal(1, await factory.QueryDbAsync(db => db.MfaSecrets.CountAsync()));

        var stale = await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(first) });
        Assert.Equal(HttpStatusCode.BadRequest, stale.Status);

        var fresh = await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(second) });
        Assert.Equal(HttpStatusCode.OK, fresh.Status);
    }

    [Fact]
    public async Task Re_enrolling_a_live_authenticator_is_409()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        var secret = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();
        await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(secret) });

        var response = await client.PostAsync(MfaEnrollPath);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal("mfa_already_enabled", response.ErrorCode);
    }

    [Fact]
    public async Task Verifying_elevates_the_session_to_privileged()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        Assert.False((await SingleSessionAsync(factory)).IsPrivileged);

        var secret = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();
        await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(secret) });

        var session = await SingleSessionAsync(factory);
        Assert.True(session.IsPrivileged);

        // A privileged session must not keep the 14-day client expiry it was issued with.
        Assert.True(session.ExpiresAt < DateTimeOffset.UtcNow.AddHours(13));
    }

    [Theory]
    [InlineData(MfaEnrollPath)]
    [InlineData(MfaVerifyPath)]
    public async Task Mfa_endpoints_require_a_session(string path)
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(path, new { code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("unauthorized", response.ErrorCode);
    }

    [Fact]
    public async Task A_persisted_key_ring_keeps_the_secret_readable_across_a_restart()
    {
        // The Data Protection fix: with the framework default the key ring dies with the process, so
        // a redeploy would make every stored MFA secret permanently undecryptable.
        var keyRing = NewKeyRingPath();

        var before = CreateFactory(dataProtectionKeyRingPath: keyRing);
        var client = before.CreateApiClient();

        await LoginAsync(before, client, Email);
        var secret = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();
        var token = client.SessionToken!;

        await before.DisposeAsync();

        // Same database, same key ring directory, brand new host.
        var after = CreateFactory(dataProtectionKeyRingPath: keyRing);
        var reconnected = after.CreateApiClient();
        reconnected.SessionToken = token;

        var response = await reconnected.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(secret) });

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull((await after.QueryDbAsync(db => db.MfaSecrets.SingleAsync())).EnabledAt);
    }

    [Fact]
    public async Task Losing_the_key_ring_makes_the_stored_secret_unusable()
    {
        // The negative control for the test above: prove it is the shared key ring doing the work,
        // not something else. A host that cannot decrypt the secret must fail the code, never pass it.
        var before = CreateFactory();
        var client = before.CreateApiClient();

        await LoginAsync(before, client, Email);
        var secret = (await client.PostAsync(MfaEnrollPath)).Data!["secret"]!.GetValue<string>();
        var token = client.SessionToken!;

        await before.DisposeAsync();

        var after = CreateFactory(dataProtectionKeyRingPath: NewKeyRingPath());
        var reconnected = after.CreateApiClient();
        reconnected.SessionToken = token;

        var response = await reconnected.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(secret) });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("mfa_invalid_code", response.ErrorCode);
    }
}
