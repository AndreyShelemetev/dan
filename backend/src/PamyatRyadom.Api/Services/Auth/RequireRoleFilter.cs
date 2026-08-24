using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>Enforces <see cref="RequireRoleAttribute"/>: resolves the session cookie into a user,
/// checks status and role, and stashes the result for the action.
///
/// MFA is a signal, not a gate. A privileged account that has not enrolled yet is still let through
/// with <c>meta.mfaSetupRequired = true</c> so the frontend can push it into enrollment — hard-blocking
/// would lock out the first administrator, who has no way to enroll before signing in.</summary>
public sealed class RequireRoleFilter : IAsyncActionFilter
{
    private const string MfaSetupRequiredKey = "mfaSetupRequired";

    private readonly IAuthService _auth;
    private readonly AuthOptions _options;
    private readonly IReadOnlyList<string> _roles;

    public RequireRoleFilter(IAuthService auth, IOptions<AuthOptions> options, string[] roles)
    {
        _auth = auth;
        _options = options.Value;
        _roles = roles;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var token = AuthCookie.Read(httpContext.Request, _options);

        var current = await _auth.GetCurrentUserAsync(token, httpContext.RequestAborted);
        if (current is null)
        {
            context.Result = Problem(
                StatusCodes.Status401Unauthorized,
                ApiError.Of("unauthorized", "Требуется вход."));
            return;
        }

        var user = current.User;

        if (user.Status != UserStatuses.Active)
        {
            context.Result = Problem(
                StatusCodes.Status403Forbidden,
                ApiError.Of("forbidden", "Учётная запись недоступна."));
            return;
        }

        if (_roles.Count > 0 && !_roles.Contains(user.Role))
        {
            context.Result = Problem(
                StatusCodes.Status403Forbidden,
                ApiError.Of("forbidden", "Недостаточно прав."));
            return;
        }

        if (current.Rotated)
        {
            // The cookie the browser sent is now worthless — hand back the rotated one immediately.
            AuthCookie.Write(httpContext.Response, _options, current.SessionToken, current.ExpiresAt);
        }

        httpContext.Items[AuthHttpContextItems.User] = user;
        httpContext.Items[AuthHttpContextItems.SessionToken] = current.SessionToken;

        var mfaSetupRequired = UserRoles.Privileged.Contains(user.Role) && !user.MfaEnabled;
        httpContext.Items[AuthHttpContextItems.MfaSetupRequired] = mfaSetupRequired;

        var executed = await next();

        if (mfaSetupRequired)
        {
            FlagMfaSetupRequired(executed);
        }
    }

    private static ObjectResult Problem(int statusCode, ApiError error) =>
        new(ApiResponse<object>.Fail(error)) { StatusCode = statusCode };

    private static void FlagMfaSetupRequired(ActionExecutedContext context)
    {
        if (context.Result is not ObjectResult { Value: IApiResponse response })
        {
            return;
        }

        response.Meta = MergeMeta(response.Meta, MfaSetupRequiredKey, true);
    }

    /// <summary>Adds a key to whatever <c>meta</c> the action already produced, without discarding it.</summary>
    private static Dictionary<string, object?> MergeMeta(object? existing, string key, object? value)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);

        switch (existing)
        {
            case null:
                break;
            case IDictionary<string, object?> dictionary:
                foreach (var pair in dictionary)
                {
                    merged[pair.Key] = pair.Value;
                }

                break;
            default:
                // An anonymous or typed meta object: fold its public properties in so the action's own
                // metadata survives.
                foreach (var property in existing.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length == 0 && property.CanRead)
                    {
                        merged[property.Name] = property.GetValue(existing);
                    }
                }

                break;
        }

        merged[key] = value;
        return merged;
    }
}
