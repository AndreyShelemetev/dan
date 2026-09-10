using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Dispatch;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Dispatch;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Dispatch and quality control: handing a paid order to an executor, and deciding whether what
/// came back may be shown to the client.
///
/// Approving a report is a QA action rather than a dispatcher one, and the split is enforced per
/// endpoint rather than for the controller as a whole — the person who arranged the work should
/// not be the only person who signs it off.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public sealed class AdminDispatchController : AuthorizedControllerBase
{
    private readonly IVisitService _visits;

    public AdminDispatchController(IVisitService visits) => _visits = visits;

    [HttpPost("orders/{orderId:long}/visits")]
    [RequireRole(UserRoles.Dispatcher, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Assign(
        long orderId, [FromBody] AssignVisitDto dto, CancellationToken ct) =>
        Envelope(await _visits.AssignAsync(CurrentUserId, orderId, dto, ct));

    /// <summary>Executors available to be sent somewhere. Dispatch has to pick a name, and a
    /// dropdown built from an unfiltered user list is how the wrong person gets assigned.</summary>
    [HttpGet("executors")]
    [RequireRole(UserRoles.Dispatcher, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExecutorOptionDto>>>> Executors(
        CancellationToken ct) =>
        Envelope(await _visits.ListExecutorsAsync(ct));

    [HttpGet("qa/queue")]
    [RequireRole(UserRoles.Qa, UserRoles.Dispatcher, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VisitDto>>>> QaQueue(CancellationToken ct) =>
        Envelope(await _visits.QaQueueAsync(ct));

    /// <summary>Passes the report to the client. There is no route from a filed report straight
    /// to a client that does not come through here (BR-010).</summary>
    [HttpPost("visits/{id:long}/approve")]
    [RequireRole(UserRoles.Qa, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Approve(long id, CancellationToken ct) =>
        Envelope(await _visits.ApproveAsync(CurrentUserId, id, ct));

    [HttpPost("visits/{id:long}/rework")]
    [RequireRole(UserRoles.Qa, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<VisitDto>>> SendBack(
        long id, [FromBody] ReviewDto dto, CancellationToken ct) =>
        Envelope(await _visits.SendBackAsync(CurrentUserId, id, dto.Note, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
