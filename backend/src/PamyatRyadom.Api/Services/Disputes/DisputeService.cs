using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Disputes;
using PamyatRyadom.Api.Models.Disputes;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Models.Payments;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Orders;
using PamyatRyadom.Api.Services.Payments;

namespace PamyatRyadom.Api.Services.Disputes;

public interface IDisputeService
{
    /// <summary>Opens a case behind an order's move to `disputed`. The order transition and the
    /// case are one action from the client's side — see D13/D14.</summary>
    Task<ServiceResult<DisputeDto>> OpenAsync(long userId, long orderId, string? reason, CancellationToken ct = default);

    /// <summary>The client's own dispute for this order — whichever is most recent. A resolved or
    /// rejected case is still returned, so the client can see the outcome, not just the fact that
    /// something is happening.</summary>
    Task<ServiceResult<DisputeDto>> GetForClientAsync(long userId, long orderId, CancellationToken ct = default);

    /// <summary>The queue support/finance/admin work from, optionally narrowed to one status.</summary>
    Task<ServiceResult<IReadOnlyList<AdminDisputeDto>>> ListForStaffAsync(string? status, CancellationToken ct = default);

    /// <summary>One case, for the detail view behind the queue.</summary>
    Task<ServiceResult<AdminDisputeDto>> GetForStaffAsync(long disputeId, CancellationToken ct = default);

    /// <summary>Picks up an open case — nobody else is working it while it sits unclaimed.</summary>
    Task<ServiceResult<AdminDisputeDto>> ClaimAsync(long actorId, long disputeId, CancellationToken ct = default);

    /// <summary>Resolves a case with no money involved: rework (the order goes back to
    /// <c>in_progress</c>) or a plain rejection. A refund goes through <see cref="RefundAsync"/>
    /// instead — support and admin reach this method, but only finance and admin ever reach that
    /// one, same split as everywhere else money moves.</summary>
    Task<ServiceResult<AdminDisputeDto>> ResolveAsync(
        long actorId, long disputeId, string resolutionType, string? resolutionText, CancellationToken ct = default);

    /// <summary>Resolves a case with a refund — the one path here that actually moves money, via
    /// the same <see cref="IPaymentService.RefundAsync"/> a plain admin refund uses. Null
    /// <paramref name="amountRub"/> means everything still owed; a full refund moves the order to
    /// <c>refunded</c>, a partial one leaves the work-is-done order alone.</summary>
    Task<ServiceResult<AdminDisputeDto>> RefundAsync(
        long actorId, long disputeId, decimal? amountRub, string? resolutionText, CancellationToken ct = default);
}

public sealed class DisputeService : IDisputeService
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;

    public DisputeService(AppDbContext db, IOrderService orders, IPaymentService payments)
    {
        _db = db;
        _orders = orders;
        _payments = payments;
    }

    public async Task<ServiceResult<DisputeDto>> OpenAsync(
        long userId, long orderId, string? reason, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            // 404 covers both "no such order" and "not yours" — telling them apart would confirm
            // the order exists.
            return ServiceResult<DisputeDto>.NotFound("Заказ не найден.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            // A dispute with no statement of what is wrong cannot be resolved, only argued about.
            return ServiceResult<DisputeDto>.Validation("Опишите, что не так — без этого спор не разобрать.");
        }

        var trimmedReason = reason.Trim();

        var hasLiveDispute = await _db.Disputes.AnyAsync(
            d => d.OrderId == orderId && DisputeStatuses.Live.Contains(d.Status), ct);
        if (hasLiveDispute)
        {
            return ServiceResult<DisputeDto>.Validation("По этому заказу уже открыто обращение — дождитесь решения.");
        }

        var moved = await _orders.TransitionAsync(orderId, OrderStatuses.Disputed, userId, "client", trimmedReason, ct);
        if (!moved.Succeeded)
        {
            return ServiceResult<DisputeDto>.Fail(moved.StatusCode, moved.Errors.ToArray());
        }

        var dispute = new Dispute
        {
            OrderId = orderId,
            OpenedByUserId = userId,
            Reason = trimmedReason,
        };
        _db.Disputes.Add(dispute);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<DisputeDto>.Created(Map(dispute));
    }

    public async Task<ServiceResult<DisputeDto>> GetForClientAsync(
        long userId, long orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<DisputeDto>.NotFound("Заказ не найден.");
        }

        var dispute = await _db.Disputes
            .AsNoTracking()
            .Where(d => d.OrderId == orderId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return dispute is null
            ? ServiceResult<DisputeDto>.NotFound("По этому заказу обращений не было.")
            : ServiceResult<DisputeDto>.Ok(Map(dispute));
    }

    public async Task<ServiceResult<IReadOnlyList<AdminDisputeDto>>> ListForStaffAsync(
        string? status, CancellationToken ct = default)
    {
        var query = _db.Disputes.AsNoTracking().Include(d => d.Order).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!DisputeStatuses.All.Contains(status))
            {
                return ServiceResult<IReadOnlyList<AdminDisputeDto>>.Validation("Неизвестный статус обращения.");
            }

            query = query.Where(d => d.Status == status);
        }

        var disputes = await query.OrderBy(d => d.CreatedAt).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<AdminDisputeDto>>.Ok(disputes.Select(MapForStaff).ToList());
    }

    public async Task<ServiceResult<AdminDisputeDto>> GetForStaffAsync(long disputeId, CancellationToken ct = default)
    {
        var dispute = await LoadForStaffAsync(disputeId, ct);
        return dispute is null
            ? ServiceResult<AdminDisputeDto>.NotFound("Обращение не найдено.")
            : ServiceResult<AdminDisputeDto>.Ok(MapForStaff(dispute));
    }

    public async Task<ServiceResult<AdminDisputeDto>> ClaimAsync(
        long actorId, long disputeId, CancellationToken ct = default)
    {
        var dispute = await LoadForStaffAsync(disputeId, ct, tracking: true);
        if (dispute is null)
        {
            return ServiceResult<AdminDisputeDto>.NotFound("Обращение не найдено.");
        }

        if (dispute.Status != DisputeStatuses.Open)
        {
            return ServiceResult<AdminDisputeDto>.Validation("В работу можно взять только новое обращение.");
        }

        dispute.Status = DisputeStatuses.InReview;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<AdminDisputeDto>.Ok(MapForStaff(dispute));
    }

    public async Task<ServiceResult<AdminDisputeDto>> ResolveAsync(
        long actorId, long disputeId, string resolutionType, string? resolutionText, CancellationToken ct = default)
    {
        // Money outcomes never reach here — RefundAsync is the only door for those, and it is
        // gated to finance/admin at the controller, not by re-checking a role this method was
        // never given.
        if (resolutionType != DisputeResolutionTypes.Rework && resolutionType != DisputeResolutionTypes.Rejected)
        {
            return ServiceResult<AdminDisputeDto>.Validation(
                "Этим действием можно только отправить на переделку или отклонить — для возврата есть отдельный путь.");
        }

        if (string.IsNullOrWhiteSpace(resolutionText))
        {
            return ServiceResult<AdminDisputeDto>.Validation("Решение без обоснования не принимается.");
        }

        var dispute = await LoadForStaffAsync(disputeId, ct, tracking: true);
        if (dispute is null)
        {
            return ServiceResult<AdminDisputeDto>.NotFound("Обращение не найдено.");
        }

        if (!DisputeStatuses.Live.Contains(dispute.Status))
        {
            return ServiceResult<AdminDisputeDto>.Validation("Обращение уже закрыто.");
        }

        var toOrderStatus = resolutionType == DisputeResolutionTypes.Rework
            ? OrderStatuses.InProgress
            : OrderStatuses.Completed;

        var moved = await _orders.TransitionAsync(dispute.OrderId, toOrderStatus, actorId, null, resolutionText.Trim(), ct);
        if (!moved.Succeeded)
        {
            return ServiceResult<AdminDisputeDto>.Fail(moved.StatusCode, moved.Errors.ToArray());
        }

        dispute.Status = resolutionType == DisputeResolutionTypes.Rework
            ? DisputeStatuses.Resolved
            : DisputeStatuses.Rejected;
        dispute.ResolutionType = resolutionType;
        dispute.ResolutionText = resolutionText.Trim();
        dispute.ResolvedByUserId = actorId;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<AdminDisputeDto>.Ok(MapForStaff(dispute));
    }

    public async Task<ServiceResult<AdminDisputeDto>> RefundAsync(
        long actorId, long disputeId, decimal? amountRub, string? resolutionText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resolutionText))
        {
            return ServiceResult<AdminDisputeDto>.Validation("Решение без обоснования не принимается.");
        }

        if (amountRub is <= 0)
        {
            return ServiceResult<AdminDisputeDto>.Validation("Сумма возврата должна быть больше нуля.");
        }

        var dispute = await LoadForStaffAsync(disputeId, ct, tracking: true);
        if (dispute is null)
        {
            return ServiceResult<AdminDisputeDto>.NotFound("Обращение не найдено.");
        }

        if (!DisputeStatuses.Live.Contains(dispute.Status))
        {
            return ServiceResult<AdminDisputeDto>.Validation("Обращение уже закрыто.");
        }

        var trimmedText = resolutionText.Trim();
        var refunded = await _payments.RefundAsync(actorId, dispute.OrderId, amountRub, trimmedText, ct);
        if (!refunded.Succeeded)
        {
            return ServiceResult<AdminDisputeDto>.Fail(refunded.StatusCode, refunded.Errors.ToArray());
        }

        // PaymentService.RefundAsync already moved the order to `refunded` if this drained the
        // balance; a partial refund leaves the order exactly where it was (BR: the work still
        // happened), so it is this method's job — not the payment's — to close the case out.
        var fullyRefunded = refunded.Data!.Status == PaymentStatuses.Refunded;
        if (!fullyRefunded)
        {
            var moved = await _orders.TransitionAsync(
                dispute.OrderId, OrderStatuses.Completed, actorId, null, trimmedText, ct);
            if (!moved.Succeeded)
            {
                return ServiceResult<AdminDisputeDto>.Fail(moved.StatusCode, moved.Errors.ToArray());
            }
        }

        dispute.Status = DisputeStatuses.Resolved;
        dispute.ResolutionType = fullyRefunded ? DisputeResolutionTypes.FullRefund : DisputeResolutionTypes.PartialRefund;
        dispute.ResolutionText = trimmedText;
        dispute.RefundAmountRub = fullyRefunded ? null : amountRub;
        dispute.ResolvedByUserId = actorId;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<AdminDisputeDto>.Ok(MapForStaff(dispute));
    }

    private async Task<Dispute?> LoadForStaffAsync(long disputeId, CancellationToken ct, bool tracking = false)
    {
        var query = _db.Disputes.Include(d => d.Order).Where(d => d.Id == disputeId);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    private static AdminDisputeDto MapForStaff(Dispute d) => new()
    {
        Id = d.Id,
        OrderId = d.OrderId,
        OrderNumber = d.Order?.Number ?? string.Empty,
        Reason = d.Reason,
        Status = d.Status,
        StatusLabel = DisputeStatusPresentation.StatusLabel(d.Status),
        ResolutionType = d.ResolutionType,
        ResolutionTypeLabel = DisputeStatusPresentation.ResolutionLabel(d.ResolutionType),
        ResolutionText = d.ResolutionText,
        RefundAmountRub = d.RefundAmountRub,
        OpenedByUserId = d.OpenedByUserId,
        ResolvedByUserId = d.ResolvedByUserId,
        ResolvedAt = d.ResolvedAt,
        CreatedAt = d.CreatedAt,
    };

    private static DisputeDto Map(Dispute d) => new()
    {
        Id = d.Id,
        OrderId = d.OrderId,
        Reason = d.Reason,
        Status = d.Status,
        StatusLabel = DisputeStatusPresentation.StatusLabel(d.Status),
        ResolutionType = d.ResolutionType,
        ResolutionTypeLabel = DisputeStatusPresentation.ResolutionLabel(d.ResolutionType),
        ResolutionText = d.ResolutionText,
        RefundAmountRub = d.RefundAmountRub,
        ResolvedAt = d.ResolvedAt,
        CreatedAt = d.CreatedAt,
    };
}
