using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Auth;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Account administration: the only way to give someone a staff or executor account outside
/// Development, where <c>DevAccountSeeder</c> does the job instead.
///
/// Restricted to admin and superadmin, same as <see cref="AdminCatalogController"/> — who can sign
/// in as what is not a dispatcher's call.
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
[RequireRole(UserRoles.Admin, UserRoles.Superadmin)]
public sealed class AdminUsersController : AuthorizedControllerBase
{
    private readonly IAdminUsersService _users;

    public AdminUsersController(IAdminUsersService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminUserDto>>>> List(
        [FromQuery] string? role, CancellationToken ct) =>
        Envelope(await _users.ListAsync(role, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> Create(
        [FromBody] CreateAdminUserDto dto, CancellationToken ct) =>
        Envelope(await _users.CreateAsync(CurrentUserId, CurrentUser.Role, dto, BuildContext(), ct));

    [HttpPatch("{id:long}/role")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> ChangeRole(
        long id, [FromBody] ChangeUserRoleDto dto, CancellationToken ct) =>
        Envelope(await _users.ChangeRoleAsync(CurrentUserId, CurrentUser.Role, id, dto, BuildContext(), ct));

    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> Deactivate(long id, CancellationToken ct) =>
        Envelope(await _users.DeactivateAsync(CurrentUserId, CurrentUser.Role, id, BuildContext(), ct));

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> Activate(long id, CancellationToken ct) =>
        Envelope(await _users.ActivateAsync(CurrentUserId, CurrentUser.Role, id, BuildContext(), ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
