using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Controllers;
using PamyatRyadom.Api.Dtos.Auth;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// A test-only endpoint pair that puts <see cref="RequireRoleFilter"/> under a *narrowed* role set.
/// Every production endpoint today is <c>[RequireRole]</c> with no arguments ("any signed-in user"),
/// so without this the 403-for-the-wrong-role branch would have no coverage at all. It lives in the
/// test assembly and is only reachable because <see cref="PamyatApiFactory"/> adds that assembly as
/// an MVC application part — nothing about the production surface changes.
/// </summary>
[ApiController]
[Route("api/v1/test/role-probe")]
[Produces("application/json")]
public sealed class RoleProbeController : AuthorizedControllerBase
{
    /// <summary>Any active signed-in user, whatever their role.</summary>
    [HttpGet("any")]
    [RequireRole]
    public ActionResult<ApiResponse<AuthUserDto>> Any() => Ok(ApiResponse<AuthUserDto>.Ok(CurrentUser));

    /// <summary>Dispatch-desk roles only.</summary>
    [HttpGet("dispatch")]
    [RequireRole(UserRoles.Dispatcher, UserRoles.Admin)]
    public ActionResult<ApiResponse<AuthUserDto>> Dispatch() => Ok(ApiResponse<AuthUserDto>.Ok(CurrentUser));

    /// <summary>Same as <see cref="Any"/>, but the action returns a meta of its own — so a test can
    /// prove the filter's <c>mfaSetupRequired</c> flag is merged into it rather than replacing it.</summary>
    [HttpGet("any-with-meta")]
    [RequireRole]
    public ActionResult<ApiResponse<AuthUserDto>> AnyWithMeta() =>
        Ok(ApiResponse<AuthUserDto>.Ok(CurrentUser, new { probe = "value" }));
}
