using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Payments;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Models.Payments;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Orders;

namespace PamyatRyadom.Api.Services.Payments;

public interface IPaymentService
{
    /// <summary>Starts (or resumes) paying for an order. Safe to call repeatedly: an attempt
    /// that is still live is returned as it is rather than duplicated.</summary>
    Task<ServiceResult<PaymentDto>> StartAsync(long userId, long orderId, CancellationToken ct = default);

    /// <summary>Re-reads the payment from the provider and applies the result. The only path by
    /// which an order becomes paid.</summary>
    Task<ServiceResult<PaymentDto>> SyncAsync(long paymentId, CancellationToken ct = default);

    /// <summary>Handles a provider callback. Takes the reference only — never a status, never an
    /// amount — because a callback is a hint to look, not evidence of anything (BR-007).</summary>
    Task<ServiceResult<object>> HandleCallbackAsync(string providerPaymentId, CancellationToken ct = default);

    Task<ServiceResult<PaymentDto>> GetForOrderAsync(long userId, long orderId, CancellationToken ct = default);

    Task<ServiceResult<PaymentDto>> RefundAsync(
        long actorId, long orderId, decimal? amountRub, string? reason, CancellationToken ct = default);
}

/// <summary>
/// Taking money for an order, and giving it back.
///
/// Two rules shape everything here. A payment's status is whatever the provider says when asked
/// directly — a callback only triggers the asking. And a charge is keyed on a reference we
/// generate once, so any retry anywhere in the chain lands on the same payment rather than a
/// second one.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPaymentProvider _provider;
    private readonly IOrderService _orders;
    private readonly ILogger<PaymentService> _logger;
    private readonly PaymentOptions _options;

    public PaymentService(
        AppDbContext db,
        IPaymentProvider provider,
        IOrderService orders,
        Microsoft.Extensions.Options.IOptions<PaymentOptions> options,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _provider = provider;
        _orders = orders;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<PaymentDto>> StartAsync(long userId, long orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Estimates)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<PaymentDto>.NotFound("Заказ не найден.");
        }

        if (order.Status != OrderStatuses.AwaitingPayment)
        {
            return ServiceResult<PaymentDto>.Validation(
                "Оплатить можно только заказ с принятой сметой.");
        }

        var accepted = order.Estimates.FirstOrDefault(e => e.Status == EstimateStatuses.Accepted);
        if (accepted is null)
        {
            // Belt and braces: awaiting_payment is only reachable through acceptance, so this
            // means the two have drifted apart and charging would be guesswork.
            _logger.LogError("Order {Number} is awaiting payment with no accepted estimate", order.Number);
            return ServiceResult<PaymentDto>.Validation("По заказу нет принятой сметы.");
        }

        // An attempt that is still live is the same attempt. Creating a second one would leave
        // two confirmation links for one order, and the client holding whichever they clicked.
        var live = await _db.Payments
            .Where(p => p.OrderId == orderId && !PaymentStatuses.Terminal.Contains(p.Status))
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (live is not null && live.EstimateVersion == accepted.Version)
        {
            return ServiceResult<PaymentDto>.Ok(Map(live, isActive: true));
        }

        // A live attempt against a superseded estimate is abandoned rather than reused: it is for
        // an amount the client is no longer agreeing to.
        if (live is not null)
        {
            live.Status = PaymentStatuses.Canceled;
            live.FailureReason = "estimate_superseded";
            live.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var payment = new Payment
        {
            OrderId = order.Id,
            OrderRef = $"{order.Number}-v{accepted.Version}-{Guid.NewGuid():N}"[..40],
            Provider = _provider.Name,
            IdempotenceKey = Guid.NewGuid().ToString("N"),
            AmountRub = accepted.TotalRub,
            EstimateVersion = accepted.Version,
            Status = PaymentStatuses.Pending,
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        try
        {
            var created = await _provider.CreateAsync(new CreatePaymentRequest(
                OrderRef: payment.OrderRef,
                AmountRub: payment.AmountRub,
                // The order number and nothing else. A description is printed on a bank
                // statement, and the deceased's name is not going there.
                Description: $"Заказ {order.Number}",
                ReturnUrl: $"{_options.ReturnUrlBase.TrimEnd('/')}/cabinet/orders/{order.Id}",
                IdempotenceKey: payment.IdempotenceKey), ct);

            payment.ProviderPaymentId = created.Id;
            payment.Status = created.Status;
            payment.ConfirmationUrl = created.ConfirmationUrl;
            payment.LastCheckedAt = DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            payment.Status = PaymentStatuses.Failed;
            payment.FailureReason = "provider_unavailable";
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Provider {Provider} refused to create a payment for {Number}",
                _provider.Name, order.Number);

            return ServiceResult<PaymentDto>.Fail(
                StatusCodes.Status502BadGateway,
                ApiError.Of("payment_provider_unavailable",
                    "Платёжный сервис сейчас недоступен. Попробуйте через несколько минут."));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PaymentDto>.Created(Map(payment, isActive: true));
    }

    public async Task<ServiceResult<PaymentDto>> SyncAsync(long paymentId, CancellationToken ct = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null)
        {
            return ServiceResult<PaymentDto>.NotFound("Платёж не найден.");
        }

        if (payment.ProviderPaymentId is null)
        {
            return ServiceResult<PaymentDto>.Ok(Map(payment, isActive: false));
        }

        if (PaymentStatuses.IsTerminal(payment.Status))
        {
            return ServiceResult<PaymentDto>.Ok(Map(payment, isActive: false));
        }

        ProviderPayment view;
        try
        {
            view = await _provider.GetAsync(payment.ProviderPaymentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-read payment {PaymentId} from {Provider}",
                payment.Id, payment.Provider);
            return ServiceResult<PaymentDto>.Fail(
                StatusCodes.Status502BadGateway,
                ApiError.Of("payment_provider_unavailable", "Не удалось проверить статус платежа."));
        }

        await ApplyAsync(payment, view, ct);
        return ServiceResult<PaymentDto>.Ok(Map(payment, isActive: !PaymentStatuses.IsTerminal(payment.Status)));
    }

    public async Task<ServiceResult<object>> HandleCallbackAsync(
        string providerPaymentId, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ProviderPaymentId == providerPaymentId, ct);

        if (payment is null)
        {
            // 200 anyway: an unknown reference is not something the provider can fix by retrying,
            // and a non-2xx here earns an exponential-backoff storm for nothing.
            _logger.LogWarning("Callback for unknown payment {ProviderPaymentId}", providerPaymentId);
            return ServiceResult<object>.Ok(new { received = true });
        }

        var result = await SyncAsync(payment.Id, ct);
        return result.Succeeded
            ? ServiceResult<object>.Ok(new { received = true })
            : ServiceResult<object>.Ok(new { received = true, deferred = true });
    }

    public async Task<ServiceResult<PaymentDto>> GetForOrderAsync(
        long userId, long orderId, CancellationToken ct = default)
    {
        var owns = await _db.Orders.AnyAsync(o => o.Id == orderId && o.CustomerUserId == userId, ct);
        if (!owns)
        {
            return ServiceResult<PaymentDto>.NotFound("Заказ не найден.");
        }

        var payment = await _db.Payments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return payment is null
            ? ServiceResult<PaymentDto>.NotFound("По заказу нет платежей.")
            : ServiceResult<PaymentDto>.Ok(Map(payment, isActive: !PaymentStatuses.IsTerminal(payment.Status)));
    }

    public async Task<ServiceResult<PaymentDto>> RefundAsync(
        long actorId, long orderId, decimal? amountRub, string? reason, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Where(p => p.OrderId == orderId && p.Status == PaymentStatuses.Succeeded)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (payment is null)
        {
            return ServiceResult<PaymentDto>.NotFound("По заказу нет оплаченного платежа.");
        }

        var refundable = payment.AmountRub - payment.RefundedRub;
        var amount = amountRub ?? refundable;

        if (amount <= 0)
        {
            return ServiceResult<PaymentDto>.Validation("Сумма возврата должна быть больше нуля.");
        }

        if (amount > refundable)
        {
            return ServiceResult<PaymentDto>.Validation(
                $"К возврату доступно {refundable:0.00} ₽.");
        }

        try
        {
            // Keyed on the payment and the running refunded total, so retrying the same refund
            // is the same request and returning the rest afterwards is a different one.
            var key = $"refund-{payment.Id}-{payment.RefundedRub:0.00}-{amount:0.00}";
            await _provider.RefundAsync(payment.ProviderPaymentId!, amount, key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refund failed for payment {PaymentId}", payment.Id);
            return ServiceResult<PaymentDto>.Fail(
                StatusCodes.Status502BadGateway,
                ApiError.Of("refund_failed", "Не удалось выполнить возврат. Платёж не изменён."));
        }

        payment.RefundedRub += amount;
        payment.RefundedAt = DateTimeOffset.UtcNow;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        if (payment.RefundedRub >= payment.AmountRub)
        {
            payment.Status = PaymentStatuses.Refunded;
        }

        await _db.SaveChangesAsync(ct);

        // A full refund closes the order; a partial one is a price correction on work that still
        // happened, and must not.
        if (payment.Status == PaymentStatuses.Refunded)
        {
            var moved = await _orders.TransitionAsync(
                orderId, OrderStatuses.Refunded, actorId, null, reason ?? "refund", ct);

            if (!moved.Succeeded)
            {
                _logger.LogError("Refunded payment {PaymentId} but order {OrderId} would not move",
                    payment.Id, orderId);
            }
        }

        return ServiceResult<PaymentDto>.Ok(Map(payment, isActive: false));
    }

    /// <summary>Writes the provider's view onto our row, and moves the order if it just became
    /// paid. Idempotent: applying the same view twice changes nothing the second time.</summary>
    private async Task ApplyAsync(Payment payment, ProviderPayment view, CancellationToken ct)
    {
        var wasPaid = payment.Status == PaymentStatuses.Succeeded;

        payment.Status = view.Status;
        payment.RefundedRub = view.RefundedRub;
        payment.FailureReason = view.FailureReason;
        payment.LastCheckedAt = DateTimeOffset.UtcNow;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        if (view.Status == PaymentStatuses.Succeeded && !wasPaid)
        {
            payment.PaidAt = DateTimeOffset.UtcNow;

            if (view.AmountRub != payment.AmountRub)
            {
                // Never silently accept a different figure: it means our idea of the price and
                // the provider's have diverged, and the client agreed to ours.
                _logger.LogError(
                    "Payment {PaymentId} settled {Settled} against an expected {Expected}",
                    payment.Id, view.AmountRub, payment.AmountRub);
            }
        }

        await _db.SaveChangesAsync(ct);

        if (view.Status == PaymentStatuses.Succeeded && !wasPaid)
        {
            var moved = await _orders.TransitionAsync(
                payment.OrderId, OrderStatuses.Paid, null, null, "payment_confirmed", ct);

            if (!moved.Succeeded)
            {
                // The money is real either way. Loud, because it needs a human.
                _logger.LogError("Payment {PaymentId} succeeded but order {OrderId} would not move to paid",
                    payment.Id, payment.OrderId);
            }
        }
    }

    private static PaymentDto Map(Payment p, bool isActive) => new()
    {
        Id = p.Id,
        OrderId = p.OrderId,
        Status = p.Status,
        StatusLabel = LabelFor(p.Status),
        AmountRub = p.AmountRub,
        RefundedRub = p.RefundedRub,
        EstimateVersion = p.EstimateVersion,
        ConfirmationUrl = isActive ? p.ConfirmationUrl : null,
        PaidAt = p.PaidAt,
        CreatedAt = p.CreatedAt,
        IsActive = isActive,
    };

    private static string LabelFor(string status) => status switch
    {
        PaymentStatuses.Pending => "Готовим оплату",
        PaymentStatuses.WaitingForCapture => "Ожидает оплаты",
        PaymentStatuses.Succeeded => "Оплачено",
        PaymentStatuses.Canceled => "Оплата отменена",
        PaymentStatuses.Failed => "Оплата не прошла",
        PaymentStatuses.Refunded => "Деньги возвращены",
        _ => "Неизвестно",
    };
}

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>Where the provider sends the browser back to. The site's own origin — arriving
    /// there proves the client returned, never that they paid.</summary>
    public string ReturnUrlBase { get; set; } = "http://localhost:3100";
}
