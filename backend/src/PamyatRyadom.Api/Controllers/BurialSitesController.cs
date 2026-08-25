using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.BurialSites;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.BurialSites;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Burial sites — the client's own records. Every action runs behind <see cref="RequireRoleAttribute"/>
/// with no role list, i.e. "any signed-in account"; per-record access is decided inside the service,
/// because it depends on ownership and membership rather than on the caller's role.
/// </summary>
[ApiController]
[Route("api/v1/burial-sites")]
[RequireRole]
public sealed class BurialSitesController : AuthorizedControllerBase
{
    private readonly IBurialSiteService _service;

    public BurialSitesController(IBurialSiteService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BurialSiteDto>>>> List(CancellationToken ct) =>
        Envelope(await _service.ListAsync(CurrentUserId, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<BurialSiteDto>>> Get(long id, CancellationToken ct) =>
        Envelope(await _service.GetAsync(CurrentUserId, id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<BurialSiteDto>>> Create(
        [FromBody] CreateBurialSiteDto dto, CancellationToken ct) =>
        Envelope(await _service.CreateAsync(CurrentUserId, dto, ct));

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<ApiResponse<BurialSiteDto>>> Update(
        long id, [FromBody] UpdateBurialSiteDto dto, CancellationToken ct) =>
        Envelope(await _service.UpdateAsync(CurrentUserId, id, dto, ct));

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken ct) =>
        Envelope(await _service.DeleteAsync(CurrentUserId, id, ct));

    [HttpGet("{id:long}/members")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BurialSiteMemberDto>>>> ListMembers(
        long id, CancellationToken ct) =>
        Envelope(await _service.ListMembersAsync(CurrentUserId, id, ct));

    /// <summary>Invites a relative. The response carries the invitation token exactly once —
    /// only its hash is kept, so it cannot be retrieved later.</summary>
    [HttpPost("{id:long}/members")]
    public async Task<ActionResult<ApiResponse<InvitationCreatedResult>>> InviteMember(
        long id, [FromBody] InviteMemberDto dto, CancellationToken ct) =>
        Envelope(await _service.InviteMemberAsync(CurrentUserId, id, dto, ct));

    [HttpDelete("{id:long}/members/{memberId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> RevokeMember(
        long id, long memberId, CancellationToken ct) =>
        Envelope(await _service.RevokeMemberAsync(CurrentUserId, id, memberId, ct));

    [HttpPost("invitations/accept")]
    public async Task<ActionResult<ApiResponse<BurialSiteDto>>> AcceptInvitation(
        [FromBody] AcceptInvitationDto dto, CancellationToken ct) =>
        Envelope(await _service.AcceptInvitationAsync(CurrentUserId, dto.Token, ct));

    /// <summary>Maps a <see cref="ServiceResult{T}"/> onto the one response envelope the whole
    /// API uses, so the frontend has exactly one shape to parse.</summary>
    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
