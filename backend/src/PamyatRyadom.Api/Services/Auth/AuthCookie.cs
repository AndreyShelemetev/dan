using Microsoft.AspNetCore.Http;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>One place that decides how the session cookie is written, so the controller and
/// <see cref="RequireRoleFilter"/> (which re-writes it after a token rotation) can never drift apart.
/// HttpOnly keeps it out of JavaScript, Secure keeps it off plaintext connections, and SameSite=Lax
/// blocks cross-site POSTs while still surviving a normal top-level navigation back to the site.</summary>
internal static class AuthCookie
{
    public static string Read(HttpRequest request, AuthOptions options) =>
        request.Cookies.TryGetValue(options.CookieName, out var token) ? token : string.Empty;

    public static void Write(HttpResponse response, AuthOptions options, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(options.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt,
            Path = "/"
        });

    /// <summary>Deleting a cookie only works when the attributes match the ones it was set with.</summary>
    public static void Clear(HttpResponse response, AuthOptions options) =>
        response.Cookies.Delete(options.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
}
