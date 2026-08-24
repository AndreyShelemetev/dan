using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PamyatRyadom.Api.Dtos.Auth;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>The OTP throttle and the plumbing it needs.
///
/// The partition is IP *and* destination: per-IP alone lets one attacker email-bomb many addresses,
/// per-destination alone lets one address be attacked from a botnet. A RateLimitPartition factory is
/// synchronous and cannot read the JSON body, so <see cref="CaptureOtpDestinationAsync"/> runs as
/// middleware ahead of the rate limiter, peeks the buffered body once, and leaves the normalized
/// destination in HttpContext.Items for <see cref="BuildOtpPartitionKey"/>.</summary>
internal static class AuthRateLimitPolicies
{
    /// <summary>Policy name used by <c>[EnableRateLimiting]</c> on the request-code endpoint.</summary>
    public const string RequestOtp = "auth-otp";

    public const string RequestOtpPath = "/api/v1/auth/otp/request";

    public const int RequestOtpPermitLimit = 5;

    public static readonly TimeSpan RequestOtpWindow = TimeSpan.FromMinutes(10);

    private const string DestinationItemKey = "PamyatRyadom.Auth.RateLimit.Destination";

    /// <summary>A login request body is tiny; anything larger is not one and is left for the action to
    /// reject.</summary>
    private const long MaxPeekBytes = 4 * 1024;

    public static async Task CaptureOtpDestinationAsync(HttpContext context)
    {
        var request = context.Request;

        if (!HttpMethods.IsPost(request.Method) ||
            !request.Path.StartsWithSegments(RequestOtpPath, StringComparison.OrdinalIgnoreCase) ||
            !request.HasJsonContentType() ||
            request.ContentLength is null or 0 or > MaxPeekBytes)
        {
            return;
        }

        request.EnableBuffering();
        try
        {
            var body = await request.ReadFromJsonAsync<RequestOtpDto>(context.RequestAborted);
            if (!string.IsNullOrWhiteSpace(body?.Destination))
            {
                context.Items[DestinationItemKey] = body.Destination.Trim().ToLowerInvariant();
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or BadHttpRequestException)
        {
            // Malformed body: model binding will produce the 400. Fall back to an IP-only partition.
        }
        finally
        {
            // Rewind, or model binding downstream would see an empty body.
            request.Body.Position = 0;
        }
    }

    /// <summary>The destination is hashed into the key so no email/phone ends up in limiter state or
    /// in anything that dumps it.</summary>
    public static string BuildOtpPartitionKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var destination = context.Items.TryGetValue(DestinationItemKey, out var value) && value is string text
            ? SecretHasher.HashHighEntropy(text)[..16]
            : "unknown";

        return $"{ip}|{destination}";
    }
}
