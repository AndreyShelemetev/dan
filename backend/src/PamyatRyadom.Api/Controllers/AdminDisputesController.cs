using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Disputes;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Disputes;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Working a dispute from the support/finance/admin side.
///
/// Every action needs a role, but not the same one: support and admin triage and decide the
/// non-money outcomes (rework, rejection); the refund itself is finance/admin only, same split
/// as the plain admin refund on <see cref="PaymentsController"/> — a dispatcher prices work, a
/// support agent hears a complaint, and neither of them moves money.
/// </summary>
[ApiController]
[Route("api/v1/admin/disputes")]
public sealed class AdminDisputesController : AuthorizedControllerBase
{
    private readonly IDisputeService _disputes;

    public AdminDisputesController(IDisputeService disputes) => _disputes = disputes;

    /// <summary>The queue, optionally narrowed to one status.</summary>
    [HttpGet]
    [RequireRole(UserRoles.Support, UserRoles.Finance, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminDisputeDto>>>> List(
        [FromQuery] string? status, CancellationToken ct) =>
        Envelope(await _disputes.ListForStaffAsync(status, ct));

    /// <summary>One case, for the detail view behind the queue.</summary>
    [HttpGet("{id:long}")]
    [RequireRole(UserRoles.Support, UserRoles.Finance, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<AdminDisputeDto>>> Get(long id, CancellationToken ct) =>
        Envelope(await _disputes.GetForStaffAsync(id, ct));

    /// <summary>Picks up an open case.</summary>
    [HttpPost("{id:long}/claim")]
    [RequireRole(UserRoles.Support, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<AdminDisputeDto>>> Claim(long id, CancellationToken ct) =>
        Envelope(await _disputes.ClaimAsync(CurrentUserId, id, ct));

    /// <summary>Resolves with no money involved: rework, or a plain rejection.</summary>
    [HttpPost("{id:long}/resolve")]
    [RequireRole(UserRoles.Support, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<AdminDisputeDto>>> Resolve(
        long id, [FromBody] ResolveDisputeDto dto, CancellationToken ct) =>
        Envelope(await _disputes.ResolveAsync(CurrentUserId, id, dto.ResolutionType, dto.ResolutionText, ct));

    /// <summary>Resolves with a refund. Finance and admin only — support prepares the case, this
    /// is where the money actually moves.</summary>
    [HttpPost("{id:long}/refund")]
    [RequireRole(UserRoles.Finance, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<AdminDisputeDto>>> Refund(
        long id, [FromBody] RefundDisputeDto dto, CancellationToken ct) =>
        Envelope(await _disputes.RefundAsync(CurrentUserId, id, dto.AmountRub, dto.ResolutionText, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
