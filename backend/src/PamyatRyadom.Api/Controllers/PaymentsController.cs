using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Payments;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Payments;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Paying for an order, and getting the money back.
///
/// The callback endpoint is deliberately open — a provider cannot present a session cookie — and
/// deliberately trusts nothing in the body beyond the reference. Everything it decides comes from
/// re-reading the payment from the provider.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class PaymentsController : AuthorizedControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments) => _payments = payments;

    /// <summary>Starts or resumes paying. Repeated calls return the live attempt, not a new one.</summary>
    [HttpPost("orders/{orderId:long}/payments")]
    [RequireRole]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Start(long orderId, CancellationToken ct) =>
        Envelope(await _payments.StartAsync(CurrentUserId, orderId, ct));

    /// <summary>The latest attempt for an order, for the client's own order page.</summary>
    [HttpGet("orders/{orderId:long}/payments/latest")]
    [RequireRole]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Latest(long orderId, CancellationToken ct) =>
        Envelope(await _payments.GetForOrderAsync(CurrentUserId, orderId, ct));

    /// <summary>Asks the provider where a payment stands and applies the answer. Called when the
    /// client returns from the provider's page — the return itself proves nothing.</summary>
    [HttpPost("payments/{paymentId:long}/sync")]
    [RequireRole]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Sync(long paymentId, CancellationToken ct) =>
        Envelope(await _payments.SyncAsync(paymentId, ct));

    /// <summary>Provider callback. Unauthenticated by necessity, and authoritative about nothing:
    /// the reference is used to look the payment up, and the status comes from re-fetching it.</summary>
    [HttpPost("payments/callback")]
    public async Task<ActionResult<ApiResponse<object>>> Callback(
        [FromBody] ProviderCallbackDto dto, CancellationToken ct)
    {
        var reference = dto.Object?.Id ?? dto.PaymentId;

        if (string.IsNullOrWhiteSpace(reference))
        {
            // 200: a body we cannot read will not become readable on the fourth retry.
            return Ok(ApiResponse<object>.Ok(new { received = true }));
        }

        return Envelope(await _payments.HandleCallbackAsync(reference, ct));
    }

    /// <summary>Returns money. Finance and admin only — a dispatcher prices work, they do not
    /// move money.</summary>
    [HttpPost("admin/orders/{orderId:long}/refund")]
    [RequireRole(UserRoles.Finance, UserRoles.Admin, UserRoles.Superadmin)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Refund(
        long orderId, [FromBody] RefundRequestDto dto, CancellationToken ct) =>
        Envelope(await _payments.RefundAsync(CurrentUserId, orderId, dto.AmountRub, dto.Reason, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}

/// <summary>The shape of a provider callback, as far as we read it — the reference, and nothing
/// else. Every other field a provider sends is ignored on purpose.</summary>
public sealed class ProviderCallbackDto
{
    public string? PaymentId { get; init; }
    public CallbackObject? Object { get; init; }

    public sealed class CallbackObject
    {
        public string? Id { get; init; }
    }
}
