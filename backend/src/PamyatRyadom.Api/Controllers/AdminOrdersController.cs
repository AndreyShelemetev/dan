using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Orders;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Orders;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// The dispatcher's queue: triage a request, price it, send it to the client.
///
/// Open to dispatchers as well as admins — pricing an order is the dispatcher's day job, unlike
/// editing the catalogue, which sets the terms for everyone.
/// </summary>
[ApiController]
[Route("api/v1/admin/orders")]
[RequireRole(UserRoles.Dispatcher, UserRoles.Admin, UserRoles.Superadmin)]
public sealed class AdminOrdersController : AuthorizedControllerBase
{
    private readonly IEstimateService _estimates;

    public AdminOrdersController(IEstimateService estimates) => _estimates = estimates;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrderSummaryDto>>>> Queue(
        [FromQuery] string? status, CancellationToken ct) =>
        Envelope(await _estimates.QueueAsync(status, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Get(long id, CancellationToken ct) =>
        Envelope(await _estimates.GetAsync(id, ct));

    [HttpPost("{id:long}/estimates")]
    public async Task<ActionResult<ApiResponse<EstimateDto>>> CreateDraft(
        long id, [FromBody] SaveEstimateDto dto, CancellationToken ct) =>
        Envelope(await _estimates.CreateDraftAsync(CurrentUserId, id, dto, ct));

    [HttpPatch("{id:long}/estimates/{version:int}")]
    public async Task<ActionResult<ApiResponse<EstimateDto>>> UpdateDraft(
        long id, int version, [FromBody] SaveEstimateDto dto, CancellationToken ct) =>
        Envelope(await _estimates.UpdateDraftAsync(CurrentUserId, id, version, dto, ct));

    [HttpPost("{id:long}/estimates/{version:int}/publish")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Publish(
        long id, int version, CancellationToken ct) =>
        Envelope(await _estimates.PublishAsync(CurrentUserId, id, version, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
