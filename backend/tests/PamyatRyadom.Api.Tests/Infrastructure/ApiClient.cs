using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Net.Http.Headers;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// A browser-shaped HTTP client for the auth endpoints: it carries the session cookie forward the way
/// a browser would, but keeps it in a plain settable property so a test can hold on to a token across
/// a rotation, present deliberate garbage, or send nothing at all.
///
/// Cookies are handled here rather than by <c>HttpClientHandler</c>'s cookie container because the
/// session cookie is marked <c>Secure</c>, which a container would refuse to send back over the
/// TestServer's plain-HTTP base address.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly string _cookieName;

    public ApiClient(HttpClient http, string cookieName)
    {
        _http = http;
        _cookieName = cookieName;
    }

    /// <summary>The token sent as the session cookie on the next request; null sends no cookie.
    /// Updated automatically from every <c>Set-Cookie</c> the server sends.</summary>
    public string? SessionToken { get; set; }

    public Task<ApiResult> GetAsync(string path) => SendAsync(HttpMethod.Get, path, body: null);

    public Task<ApiResult> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            // A serialized string (not JsonContent) so Content-Length is set: the OTP rate limiter's
            // destination-capture middleware skips bodies of unknown length, which would silently
            // collapse its (IP, destination) partition down to IP-only.
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        if (SessionToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{_cookieName}={SessionToken}");
        }

        using var response = await _http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        var result = new ApiResult(response.StatusCode, raw, ReadAuthCookie(response, _cookieName), response.Content.Headers.ContentType);

        if (result.SessionCookieCleared)
        {
            SessionToken = null;
        }
        else if (result.SetSessionToken is not null)
        {
            SessionToken = result.SetSessionToken;
        }

        return result;
    }

    private static SetCookieHeaderValue? ReadAuthCookie(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
        {
            return null;
        }

        return SetCookieHeaderValue.ParseList(values.ToList())
            .LastOrDefault(cookie => string.Equals(cookie.Name.Value, cookieName, StringComparison.Ordinal));
    }
}

/// <summary>One HTTP response, pre-parsed into the <c>{ data, meta, errors }</c> envelope every
/// endpoint returns.</summary>
public sealed class ApiResult
{
    public ApiResult(HttpStatusCode status, string raw, SetCookieHeaderValue? authCookie, System.Net.Http.Headers.MediaTypeHeaderValue? contentType)
    {
        Status = status;
        Raw = raw;
        AuthCookie = authCookie;
        ContentType = contentType?.MediaType;
        Json = string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw);
    }

    public HttpStatusCode Status { get; }

    public string Raw { get; }

    public string? ContentType { get; }

    public JsonNode? Json { get; }

    public JsonNode? Data => Json?["data"];

    public JsonNode? Meta => Json?["meta"];

    public JsonNode? Errors => Json?["errors"];

    /// <summary>The <c>code</c> of the first error, or null on a success envelope.</summary>
    public string? ErrorCode => Errors?[0]?["code"]?.GetValue<string>();

    /// <summary>The session cookie this response set, if any (including a deletion).</summary>
    public SetCookieHeaderValue? AuthCookie { get; }

    /// <summary>The new session token this response handed out — null when it set no cookie, or when
    /// it cleared the existing one.</summary>
    public string? SetSessionToken =>
        SessionCookieCleared || AuthCookie is null || AuthCookie.Value.Length == 0
            ? null
            : AuthCookie.Value.ToString();

    /// <summary>True when the response deleted the session cookie (logout writes an empty value with
    /// an expiry in the past).</summary>
    public bool SessionCookieCleared =>
        AuthCookie is not null &&
        (AuthCookie.Value.Length == 0 || AuthCookie.Expires <= DateTimeOffset.UnixEpoch);
}
