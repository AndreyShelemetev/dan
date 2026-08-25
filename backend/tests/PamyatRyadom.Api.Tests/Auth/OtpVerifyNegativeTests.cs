using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>Everything <c>POST /api/v1/auth/otp/verify</c> has to refuse: a wrong code, an expired
/// one, one that was already spent, and one whose attempt budget is gone.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpVerifyNegativeTests : AuthIntegrationTest
{
    private const string Email = "verify.negative@example.com";

    public OtpVerifyNegativeTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task A_wrong_code_is_rejected_and_counts_as_an_attempt()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, WrongCode(code)));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("invalid_code", response.ErrorCode);
        Assert.Null(response.SetSessionToken);

        var otp = await factory.QueryDbAsync(db => db.OtpCodes.SingleAsync());
        Assert.Equal(1, otp.AttemptCount);
        Assert.Null(otp.ConsumedAt);

        Assert.Equal(0, await factory.QueryDbAsync(db => db.Users.CountAsync()));
        Assert.Equal(0, await factory.QueryDbAsync(db => db.AuthSessions.CountAsync()));

        // The correct code still works — one typo does not burn the code.
        var retry = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));
        Assert.Equal(HttpStatusCode.OK, retry.Status);
    }

    [Fact]
    public async Task An_expired_code_is_rejected()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);

        // Backdating the row is the honest way to age a code out: the service reads the clock
        // directly, and adding a time abstraction just for the test would change production code.
        await factory.WithDbAsync(async db =>
        {
            var otp = await db.OtpCodes.SingleAsync();
            otp.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("invalid_code", response.ErrorCode);
        Assert.Null(response.SetSessionToken);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task An_already_consumed_code_cannot_be_replayed()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);

        var first = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));
        Assert.Equal(HttpStatusCode.OK, first.Status);

        client.SessionToken = null;
        var replay = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));

        Assert.Equal(HttpStatusCode.BadRequest, replay.Status);
        Assert.Equal("invalid_code", replay.ErrorCode);
        Assert.Null(replay.SetSessionToken);

        // Still exactly one account and one session — the replay created nothing.
        Assert.Equal(1, await factory.QueryDbAsync(db => db.Users.CountAsync()));
        Assert.Equal(1, await factory.QueryDbAsync(db => db.AuthSessions.CountAsync()));
    }

    [Fact]
    public async Task The_code_dies_once_the_attempt_budget_is_spent()
    {
        const int maxAttempts = 3;

        var factory = CreateFactory(Settings(("Auth:MaxCodeAttempts", maxAttempts.ToString())));
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);
        var wrong = WrongCode(code);

        for (var attempt = 1; attempt < maxAttempts; attempt++)
        {
            var rejected = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, wrong));

            Assert.Equal(HttpStatusCode.BadRequest, rejected.Status);
            Assert.Equal("invalid_code", rejected.ErrorCode);
            Assert.Equal(attempt, await factory.QueryDbAsync(db => db.OtpCodes.Select(x => x.AttemptCount).SingleAsync()));
        }

        // The attempt that exhausts the budget reports it immediately.
        var exhausting = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, wrong));
        Assert.Equal(HttpStatusCode.TooManyRequests, exhausting.Status);
        Assert.Equal("too_many_attempts", exhausting.ErrorCode);

        // And from here the code is dead even when it is finally typed correctly.
        var correct = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code));
        Assert.Equal(HttpStatusCode.TooManyRequests, correct.Status);
        Assert.Equal("too_many_attempts", correct.ErrorCode);
        Assert.Null(correct.SetSessionToken);

        var otp = await factory.QueryDbAsync(db => db.OtpCodes.SingleAsync());
        Assert.Equal(maxAttempts, otp.AttemptCount);
        Assert.Null(otp.ConsumedAt);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task A_fresh_code_clears_a_burnt_attempt_budget()
    {
        var factory = CreateFactory(Settings(("Auth:MaxCodeAttempts", "1")));
        var client = factory.CreateApiClient();

        var first = await RequestCodeAsync(factory, client, Email);
        var burnt = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, WrongCode(first)));
        Assert.Equal(HttpStatusCode.TooManyRequests, burnt.Status);

        var second = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, second));

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task Verifying_without_ever_requesting_a_code_is_rejected()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, "123456"));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("invalid_code", response.ErrorCode);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    public async Task A_malformed_code_is_a_validation_error_not_an_attempt(string malformed)
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, malformed));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("validation_error", response.ErrorCode);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.OtpCodes.Select(x => x.AttemptCount).SingleAsync()));
    }

    [Fact]
    public async Task A_code_issued_for_one_purpose_does_not_work_for_another()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        // The code is hashed together with its destination and purpose, so a login code cannot be
        // replayed into the email-verification flow (or the other way round).
        var code = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code, purpose: OtpPurposes.VerifyEmail));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("invalid_code", response.ErrorCode);
    }

    [Fact]
    public async Task A_code_issued_for_one_destination_does_not_work_for_another()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify("someone.else@example.com", code));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("invalid_code", response.ErrorCode);
    }
}
