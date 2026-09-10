using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Dispatch;
using PamyatRyadom.Api.Dtos.Orders;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Orders;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// The client's own orders.
///
/// No role list: ownership decides access, not role, and the service checks it on every call.
/// </summary>
[ApiController]
[Route("api/v1/orders")]
[RequireRole]
public sealed class OrdersController : AuthorizedControllerBase
{
    private readonly IOrderService _orders;
    private readonly Services.Dispatch.IVisitService _visits;

    public OrdersController(IOrderService orders, Services.Dispatch.IVisitService visits)
    {
        _orders = orders;
        _visits = visits;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrderSummaryDto>>>> List(CancellationToken ct) =>
        Envelope(await _orders.ListForClientAsync(CurrentUserId, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Get(long id, CancellationToken ct) =>
        Envelope(await _orders.GetForClientAsync(CurrentUserId, id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Create(
        [FromBody] CreateOrderDto dto, CancellationToken ct) =>
        Envelope(await _orders.CreateAsync(CurrentUserId, dto, ct));

    [HttpPost("{id:long}/submit")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Submit(long id, CancellationToken ct) =>
        Envelope(await _orders.SubmitAsync(CurrentUserId, id, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Cancel(
        long id, [FromBody] CancelOrderDto dto, CancellationToken ct) =>
        Envelope(await _orders.CancelAsync(CurrentUserId, id, dto.Reason, ct));

    /// <summary>Accepts one estimate version. This is the moment the client agrees to a price,
    /// so the version travels in the body and is checked against what is actually published.</summary>
    [HttpPost("{id:long}/estimate-acceptance")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> AcceptEstimate(
        long id, [FromBody] AcceptEstimateDto dto, CancellationToken ct) =>
        Envelope(await _orders.AcceptEstimateAsync(CurrentUserId, id, dto.Version, ct));

    [HttpPost("{id:long}/estimate-rejection")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> RejectEstimate(
        long id, [FromBody] RejectEstimateDto dto, CancellationToken ct) =>
        Envelope(await _orders.RejectEstimateAsync(CurrentUserId, id, dto.Version, dto.Reason, ct));

    /// <summary>The photo report, once QA has approved it. Before that this is a 404 — an
    /// unreviewed report is a claim, not evidence (BR-010).</summary>
    [HttpGet("{id:long}/report")]
    public async Task<ActionResult<ApiResponse<VisitReportDto>>> Report(long id, CancellationToken ct) =>
        Envelope(await _visits.GetReportForClientAsync(CurrentUserId, id, ct));

    /// <summary>The client accepts the work. Closes the order — the warranty window still allows
    /// a complaint afterwards (BR-012).</summary>
    [HttpPost("{id:long}/acceptance")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> AcceptWork(long id, CancellationToken ct) =>
        Envelope(await _orders.AcceptWorkAsync(CurrentUserId, id, ct));

    /// <summary>The client is not satisfied. A reason is required: a dispute with no statement of
    /// what is wrong cannot be resolved, only argued about.</summary>
    [HttpPost("{id:long}/dispute")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Dispute(
        long id, [FromBody] CancelOrderDto dto, CancellationToken ct) =>
        Envelope(await _orders.DisputeAsync(CurrentUserId, id, dto.Reason, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
