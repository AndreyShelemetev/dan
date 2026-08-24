using Microsoft.AspNetCore.Mvc;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>Requires a live session, and optionally one of <paramref name="roles"/>.
/// <c>[RequireRole]</c> with no arguments means "any active signed-in user";
/// <c>[RequireRole(UserRoles.Dispatcher, UserRoles.Admin)]</c> narrows it.
/// The work happens in <see cref="RequireRoleFilter"/> — this attribute only carries the role list.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireRoleAttribute : TypeFilterAttribute
{
    public RequireRoleAttribute(params string[] roles)
        : base(typeof(RequireRoleFilter))
    {
        Arguments = new object[] { roles };

        // Run before any other action filter, so nothing observes an unauthenticated request.
        Order = int.MinValue;
    }
}
