using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Dispatch;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Dispatch;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// The executor's own work.
///
/// Every action here is scoped to visits assigned to the caller — an executor cannot read, accept
/// or file against somebody else's job. That scoping lives in the service, not in a route
/// parameter, so there is no id a client could change to reach another executor's visit.
/// </summary>
[ApiController]
[Route("api/v1/executor/visits")]
[RequireRole(UserRoles.Executor)]
public sealed class ExecutorVisitsController : AuthorizedControllerBase
{
    private readonly IVisitService _visits;

    public ExecutorVisitsController(IVisitService visits) => _visits = visits;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VisitDto>>>> List(CancellationToken ct) =>
        Envelope(await _visits.ListForExecutorAsync(CurrentUserId, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Get(long id, CancellationToken ct) =>
        Envelope(await _visits.GetForExecutorAsync(CurrentUserId, id, ct));

    [HttpPost("{id:long}/accept")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Accept(long id, CancellationToken ct) =>
        Envelope(await _visits.AcceptAsync(CurrentUserId, id, ct));

    [HttpPost("{id:long}/decline")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Decline(
        long id, [FromBody] ReviewDto dto, CancellationToken ct) =>
        Envelope(await _visits.DeclineAsync(CurrentUserId, id, dto.Note, ct));

    [HttpPost("{id:long}/start")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Start(long id, CancellationToken ct) =>
        Envelope(await _visits.StartAsync(CurrentUserId, id, ct));

    /// <summary>Files the report. Refused unless every checklist line is answered and both a
    /// "before" and an "after" photograph are in place — the report is the deliverable.</summary>
    [HttpPost("{id:long}/report")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Submit(
        long id, [FromBody] SubmitReportDto dto, CancellationToken ct) =>
        Envelope(await _visits.SubmitReportAsync(CurrentUserId, id, dto, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
