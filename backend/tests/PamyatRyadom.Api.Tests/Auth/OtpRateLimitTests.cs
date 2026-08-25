using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>
/// The OTP throttle: 5 requests per 10 minutes, partitioned by (IP, destination). Per-IP alone would
/// let one attacker email-bomb many addresses; per-destination alone would let one address be
/// attacked from a botnet. The destination half of the key is captured by middleware that peeks the
/// request body, which is exactly the part that can regress silently — a partition key that quietly
/// degrades to IP-only still throttles, just against the wrong thing.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpRateLimitTests : AuthIntegrationTest
{
    /// <summary>Mirrors AuthRateLimitPolicies.RequestOtpPermitLimit (internal to the API assembly).</summary>
    private const int PermitLimit = 5;

    private const string Target = "flooded@example.com";
    private const string Bystander = "bystander@example.com";

    public OtpRateLimitTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task The_sixth_request_for_one_destination_is_429()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        for (var i = 0; i < PermitLimit; i++)
        {
            var allowed = await client.PostAsync(OtpRequestPath, OtpRequest(Target));
            Assert.Equal(HttpStatusCode.OK, allowed.Status);
        }

        var rejected = await client.PostAsync(OtpRequestPath, OtpRequest(Target));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.Status);
        Assert.Equal("rate_limited", rejected.ErrorCode);

        // The rejection uses the same envelope as everything else, so the frontend has one shape.
        Assert.Equal("application/json", rejected.ContentType);
        Assert.Null(rejected.Data);

        // The endpoint never ran: no sixth code was issued or sent.
        Assert.Equal(PermitLimit, await factory.QueryDbAsync(db => db.OtpCodes.CountAsync()));
        Assert.Equal(PermitLimit, factory.Emails.SentTo(Target).Count);
    }

    [Fact]
    public async Task A_different_destination_from_the_same_client_is_unaffected()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        for (var i = 0; i < PermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(OtpRequestPath, OtpRequest(Target))).Status);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync(OtpRequestPath, OtpRequest(Target))).Status);

        // Same connection, same everything but the destination — a neighbour must not be locked out
        // by someone else being flooded.
        var bystander = await client.PostAsync(OtpRequestPath, OtpRequest(Bystander));

        Assert.Equal(HttpStatusCode.OK, bystander.Status);
        Assert.NotNull(factory.Emails.LastCodeTo(Bystander));

        // ...and the flooded destination is still blocked.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync(OtpRequestPath, OtpRequest(Target))).Status);
    }

    [Fact]
    public async Task The_partition_ignores_casing_and_padding_of_the_destination()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        // Five requests for what is really one address, spelled differently each time.
        string[] spellings =
        [
            Target,
            Target.ToUpperInvariant(),
            $"  {Target}  ",
            $" {Target.ToUpperInvariant()}",
            Target
        ];

        foreach (var spelling in spellings)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(OtpRequestPath, OtpRequest(spelling))).Status);
        }

        var rejected = await client.PostAsync(OtpRequestPath, OtpRequest(Target));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.Status);
        Assert.Equal("rate_limited", rejected.ErrorCode);
    }

    [Fact]
    public async Task Verification_is_not_throttled_by_the_request_limit()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Target);

        // The attempt budget on the code itself (MaxCodeAttempts) is what guards verification;
        // the request limiter must not also be counting these.
        for (var i = 0; i < PermitLimit + 2; i++)
        {
            var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Target, WrongCode(code)));
            Assert.NotEqual("rate_limited", response.ErrorCode);
        }
    }
}
