using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Dtos.Auth;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Controllers;

/// <summary>Passwordless login, session lifecycle and MFA enrollment. Thin by design: validate, call
/// <see cref="IAuthService"/>, map. The one thing it owns is the session cookie — the raw token never
/// appears in a response body, only in a Set-Cookie header.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : AuthorizedControllerBase
{
    private readonly IAuthService _auth;
    private readonly AuthOptions _options;

    public AuthController(IAuthService auth, IOptions<AuthOptions> options)
    {
        _auth = auth;
        _options = options.Value;
    }

    /// <summary>Sends a one-time login code. The response is the same whether or not the destination
    /// belongs to an existing account.</summary>
    [HttpPost("otp/request")]
    [EnableRateLimiting(AuthRateLimitPolicies.RequestOtp)]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<AuthMessageDto>>> RequestOtp(
        [FromBody] RequestOtpDto request,
        CancellationToken ct)
    {
        var result = await _auth.RequestOtpAsync(
            request.Destination,
            request.Channel,
            request.Purpose,
            BuildContext(),
            ct);

        return ToActionResult(result);
    }

    /// <summary>Exchanges a valid code for a session, creating the account on first login.</summary>
    [HttpPost("otp/verify")]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<VerifyOtpResponseDto>>> VerifyOtp(
        [FromBody] VerifyOtpDto request,
        CancellationToken ct)
    {
        var result = await _auth.VerifyOtpAsync(
            request.Destination,
            request.Channel,
            request.Purpose,
            request.Code,
            BuildContext(),
            ct);

        if (!result.Succeeded || result.Data is null)
        {
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.Errors.ToArray()));
        }

        AuthCookie.Write(Response, _options, result.Data.SessionToken, result.Data.ExpiresAt);

        return Ok(ApiResponse<VerifyOtpResponseDto>.Ok(result.Data.Response));
    }

    /// <summary>The signed-in user. Deliberately not cached — this is also how a client learns that its
    /// session died, or that a privileged account still owes MFA enrollment (meta.mfaSetupRequired).</summary>
    [HttpGet("me")]
    [RequireRole]
    [ProducesResponseType(typeof(ApiResponse<AuthUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<AuthUserDto>> Me() => Ok(ApiResponse<AuthUserDto>.Ok(CurrentUser));

    /// <summary>Ends this session. Anonymous on purpose: a client holding a dead cookie must still be
    /// able to clear it.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthMessageDto>>> Logout(CancellationToken ct)
    {
        var result = await _auth.LogoutAsync(AuthCookie.Read(Request, _options), BuildContext(), ct);
        AuthCookie.Clear(Response, _options);

        return ToActionResult(result);
    }

    /// <summary>Ends every session of the current user — "sign out everywhere", and the first step
    /// after a suspected token leak.</summary>
    [HttpPost("logout-all")]
    [RequireRole]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthMessageDto>>> LogoutAll(CancellationToken ct)
    {
        var result = await _auth.RevokeAllSessionsAsync(CurrentUserId, BuildContext(), ct);
        AuthCookie.Clear(Response, _options);

        return ToActionResult(result);
    }

    /// <summary>Starts TOTP enrollment. The secret and recovery codes in this response are shown once
    /// and never retrievable again.</summary>
    [HttpPost("mfa/enroll")]
    [RequireRole]
    [ProducesResponseType(typeof(ApiResponse<MfaEnrollResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<MfaEnrollResponseDto>>> EnrollMfa(CancellationToken ct)
    {
        var result = await _auth.EnrollMfaAsync(CurrentUserId, BuildContext(), ct);
        return ToActionResult(result);
    }

    /// <summary>Confirms a TOTP code: completes a pending enrollment and elevates this session to
    /// privileged.</summary>
    [HttpPost("mfa/verify")]
    [RequireRole]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthMessageDto>>> VerifyMfa(
        [FromBody] MfaVerifyDto request,
        CancellationToken ct)
    {
        var result = await _auth.ConfirmMfaAsync(CurrentUserId, request.Code, CurrentSessionToken, BuildContext(), ct);
        return ToActionResult(result);
    }
}
