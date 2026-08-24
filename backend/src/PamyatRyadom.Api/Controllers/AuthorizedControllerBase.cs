using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Auth;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Controllers;

/// <summary>Base for controllers whose actions run behind <see cref="RequireRoleAttribute"/>. It only
/// reads what the filter already resolved — it never authenticates anything itself, so an action that
/// forgot the attribute fails loudly instead of silently running unauthenticated.</summary>
public abstract class AuthorizedControllerBase : ControllerBase
{
    /// <summary>The signed-in user, as resolved by <see cref="RequireRoleFilter"/>.</summary>
    protected AuthUserDto CurrentUser =>
        HttpContext.Items.TryGetValue(AuthHttpContextItems.User, out var value) && value is AuthUserDto user
            ? user
            : throw new InvalidOperationException(
                "No authenticated user on the request — the action is missing [RequireRole].");

    protected long CurrentUserId => CurrentUser.Id;

    /// <summary>The token that is valid right now: the rotated one when the filter rotated it during
    /// this request, not the (already dead) value in the request cookie.</summary>
    protected string? CurrentSessionToken =>
        HttpContext.Items.TryGetValue(AuthHttpContextItems.SessionToken, out var value) ? value as string : null;

    protected AuthRequestContext BuildContext() =>
        new(HttpContext.Connection.RemoteIpAddress, Request.Headers.UserAgent.ToString());

    protected ActionResult<ApiResponse<T>> ToActionResult<T>(AuthServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? Ok(ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.Errors.ToArray()));
}
