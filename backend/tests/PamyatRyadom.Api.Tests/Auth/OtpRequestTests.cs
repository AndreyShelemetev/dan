using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary><c>POST /api/v1/auth/otp/request</c>: a code is issued and delivered, and the response
/// gives away nothing about whether the destination belongs to an account.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpRequestTests : AuthIntegrationTest
{
    private const string Unknown = "nobody@example.com";
    private const string Known = "known@example.com";

    public OtpRequestTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Request_returns_ok_and_stores_a_hashed_code()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(OtpRequestPath, OtpRequest(Unknown));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Код отправлен", response.Data?["message"]?.GetValue<string>());

        var code = factory.Emails.RequireLastCodeTo(Unknown);
        Assert.Matches("^[0-9]{6}$", code);

        var otp = await factory.QueryDbAsync(db => db.OtpCodes.SingleAsync());
        Assert.Equal(Unknown, otp.Destination);
        Assert.Equal(OtpChannels.Email, otp.Channel);
        Assert.Equal(OtpPurposes.Login, otp.Purpose);
        Assert.Null(otp.ConsumedAt);
        Assert.Equal(0, otp.AttemptCount);
        Assert.True(otp.ExpiresAt > DateTimeOffset.UtcNow);

        // The raw code is emailed, never persisted: only a PBKDF2 digest reaches the database.
        Assert.StartsWith("pbkdf2-sha256:", otp.CodeHash);
        Assert.DoesNotContain(code, otp.CodeHash);
    }

    [Fact]
    public async Task Request_never_returns_the_code_in_the_response_body()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(OtpRequestPath, OtpRequest(Unknown));
        var code = factory.Emails.RequireLastCodeTo(Unknown);

        Assert.DoesNotContain(code, response.Raw);
    }

    [Fact]
    public async Task Response_is_byte_for_byte_identical_for_a_known_and_an_unknown_destination()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        // Register the "known" address for real, so the two requests differ only in whether an
        // account exists behind them.
        await LoginAsync(factory, client, Known);
        client.SessionToken = null;

        var known = await client.PostAsync(OtpRequestPath, OtpRequest(Known));
        var unknown = await client.PostAsync(OtpRequestPath, OtpRequest(Unknown));

        Assert.Equal(HttpStatusCode.OK, known.Status);
        Assert.Equal(HttpStatusCode.OK, unknown.Status);
        Assert.Equal(known.Raw, unknown.Raw);
        Assert.Equal(known.ContentType, unknown.ContentType);

        Assert.Equal(1, await factory.QueryDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Requesting_a_second_code_kills_the_first_one()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var first = await RequestCodeAsync(factory, client, Unknown);
        var second = await RequestCodeAsync(factory, client, Unknown);
        Assert.NotEqual(first, second);

        var stale = await client.PostAsync(OtpVerifyPath, OtpVerify(Unknown, first));
        Assert.Equal(HttpStatusCode.BadRequest, stale.Status);
        Assert.Equal("invalid_code", stale.ErrorCode);

        var fresh = await client.PostAsync(OtpVerifyPath, OtpVerify(Unknown, second));
        Assert.Equal(HttpStatusCode.OK, fresh.Status);
    }

    [Fact]
    public async Task Destination_is_normalized_before_it_is_stored()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(OtpRequestPath, OtpRequest("  MiXeD@Example.COM "));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("mixed@example.com", await factory.QueryDbAsync(db => db.OtpCodes.Select(x => x.Destination).SingleAsync()));
        Assert.NotNull(factory.Emails.LastCodeTo("mixed@example.com"));
    }

    [Fact]
    public async Task Sms_channel_is_rejected_with_501_and_sends_nothing()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(OtpRequestPath, OtpRequest("+79123456789", OtpChannels.Sms));

        Assert.Equal(HttpStatusCode.NotImplemented, response.Status);
        Assert.Equal("channel_not_supported", response.ErrorCode);
        Assert.Empty(factory.Emails.Sent);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.OtpCodes.CountAsync()));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("   ")]
    public async Task Malformed_email_is_rejected_with_400(string destination)
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(OtpRequestPath, OtpRequest(destination));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("validation_error", response.ErrorCode);
        Assert.Empty(factory.Emails.Sent);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.OtpCodes.CountAsync()));
    }

    [Fact]
    public async Task Request_is_audited_without_the_destination_itself()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await client.PostAsync(OtpRequestPath, OtpRequest(Unknown));

        var metadata = await LatestAuditMetadataAsync(factory, SecurityAuditEventTypes.OtpRequested);
        Assert.NotNull(metadata);
        Assert.Equal(OtpChannels.Email, metadata["channel"]?.GetValue<string>());
        Assert.Equal(OtpPurposes.Login, metadata["purpose"]?.GetValue<string>());

        // A pseudonymous key, never the address itself — audit rows carry no PII.
        Assert.NotNull(metadata["destination_key"]);
        Assert.DoesNotContain(Unknown, metadata.ToJsonString());
    }
}
