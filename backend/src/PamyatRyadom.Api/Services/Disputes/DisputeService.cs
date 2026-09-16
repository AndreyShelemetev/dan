using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Disputes;
using PamyatRyadom.Api.Models.Disputes;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Orders;

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
}

public sealed class DisputeService : IDisputeService
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orders;

    public DisputeService(AppDbContext db, IOrderService orders)
    {
        _db = db;
        _orders = orders;
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
