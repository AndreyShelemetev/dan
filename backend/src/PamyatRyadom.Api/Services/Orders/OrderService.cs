using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Orders;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.BurialSites;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Services.Orders;

public interface IOrderService
{
    Task<ServiceResult<IReadOnlyList<OrderSummaryDto>>> ListForClientAsync(long userId, CancellationToken ct = default);
    Task<ServiceResult<OrderDto>> GetForClientAsync(long userId, long orderId, CancellationToken ct = default);
    Task<ServiceResult<OrderDto>> CreateAsync(long userId, CreateOrderDto dto, CancellationToken ct = default);
    Task<ServiceResult<OrderDto>> SubmitAsync(long userId, long orderId, CancellationToken ct = default);
    Task<ServiceResult<OrderDto>> CancelAsync(long userId, long orderId, string? reason, CancellationToken ct = default);

    /// <summary>Accepts a specific estimate version. The version is required, never inferred:
    /// agreeing to "the current estimate" is not agreeing to a set of lines.</summary>
    Task<ServiceResult<OrderDto>> AcceptEstimateAsync(long userId, long orderId, int version, CancellationToken ct = default);

    Task<ServiceResult<OrderDto>> RejectEstimateAsync(long userId, long orderId, int version, string? reason, CancellationToken ct = default);

    /// <summary>The client accepts the work they have been shown. Closes the order.</summary>
    Task<ServiceResult<OrderDto>> AcceptWorkAsync(long userId, long orderId, CancellationToken ct = default);

    /// <summary>The client is not satisfied with the report. A reason is required.</summary>
    Task<ServiceResult<OrderDto>> DisputeAsync(long userId, long orderId, string? reason, CancellationToken ct = default);

    /// <summary>Moves an order between statuses through the state machine, recording who and why.
    /// The single entry point for a status change — see <see cref="OrderStateMachine"/>.</summary>
    Task<ServiceResult<OrderDto>> TransitionAsync(
        long orderId, string toStatus, long? actorUserId, string? actorRole, string? reason,
        CancellationToken ct = default);
}

public sealed class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IBurialSiteService _burialSites;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, IBurialSiteService burialSites, ILogger<OrderService> logger)
    {
        _db = db;
        _burialSites = burialSites;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<OrderSummaryDto>>> ListForClientAsync(
        long userId, CancellationToken ct = default)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.BurialSite)
            .Where(o => o.CustomerUserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<OrderSummaryDto>>.Ok(orders.Select(MapSummary).ToList());
    }

    public async Task<ServiceResult<OrderDto>> GetForClientAsync(long userId, long orderId, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);

        // 404 rather than 403 for someone else's order, for the same reason as burial sites: a
        // 403 confirms the order exists.
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> CreateAsync(long userId, CreateOrderDto dto, CancellationToken ct = default)
    {
        // Ordering means spending money, so viewing the record is not enough — the permission
        // has to be one that was granted deliberately (BurialSitePermissions.CanOrder).
        var permission = await _burialSites.ResolvePermissionAsync(userId, dto.BurialSiteId, ct);
        if (permission is null)
        {
            return ServiceResult<OrderDto>.NotFound("Место памяти не найдено.");
        }

        if (!BurialSitePermissions.CanOrder.Contains(permission))
        {
            return ServiceResult<OrderDto>.Forbidden(
                "Заказывать уход по этой карточке может владелец или участник с правом заказа.");
        }

        var package = await _db.ServicePackages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Code == dto.PackageCode && p.Status == ServicePackageStatuses.Published && p.Locale == "ru",
                ct);

        if (package is null)
        {
            return ServiceResult<OrderDto>.Validation("Такой пакет не продаётся.", new { field = "packageCode" });
        }

        if (dto.PreferredFrom is not null && dto.PreferredTo is not null && dto.PreferredFrom > dto.PreferredTo)
        {
            return ServiceResult<OrderDto>.Validation("Начало окна позже его конца.", new { field = "preferredFrom" });
        }

        var order = new Order
        {
            Number = await NextNumberAsync(ct),
            CustomerUserId = userId,
            BurialSiteId = dto.BurialSiteId,

            // The snapshot. Everything below is copied, not referenced, so a later catalogue edit
            // cannot rewrite what this client was sold.
            ServicePackageId = package.Id,
            PackageCode = package.Code,
            PackageVersion = package.Version,
            PackageTitle = package.Title,
            PackagePriceFromRub = package.PriceFromRub,
            WarrantyDays = package.WarrantyDays,

            Status = OrderStatuses.Draft,
            PreferredFrom = dto.PreferredFrom,
            PreferredTo = dto.PreferredTo,
            CustomerComment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
            Source = dto.Source,
        };

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = null,
            ToStatus = OrderStatuses.Draft,
            ActorUserId = userId,
        });

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order {Number} created by user {UserId}", order.Number, userId);

        var loaded = await LoadAsync(order.Id, ct);
        return ServiceResult<OrderDto>.Created(MapOrder(loaded!));
    }

    public async Task<ServiceResult<OrderDto>> SubmitAsync(long userId, long orderId, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        if (order.Status != OrderStatuses.Draft)
        {
            return ServiceResult<OrderDto>.Validation("Заявка уже отправлена.");
        }

        var site = await _db.BurialSites.AsNoTracking().FirstAsync(s => s.Id == order.BurialSiteId, ct);

        order.SubmittedAt = DateTimeOffset.UtcNow;

        // Submission is always its own step, even when triage follows immediately: the history
        // has to show the request was made before it was reviewed, and the machine has no edge
        // straight from draft to review.
        var submitted = Move(order, OrderStatuses.Submitted, userId, null, null);
        if (submitted is not null)
        {
            return submitted;
        }

        // The location branch. When the description is too thin to dispatch against, the order
        // goes to review instead of straight to pricing — the service does not send someone to
        // wander a cemetery and bill for the walk.
        if (site.LocationQuality == LocationQualities.Insufficient)
        {
            // Attributed to the system, not the client: they did not choose this, the description
            // they gave did — and the history should say so.
            var review = Move(order, OrderStatuses.LocationReview, null, "system", "недостаточно данных о месте");
            if (review is not null)
            {
                return review;
            }
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> CancelAsync(
        long userId, long orderId, string? reason, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        // Once money is in, cancelling is a refund workflow rather than a status flip. Letting a
        // client close a paid order from here would strand the payment.
        if (OrderStateMachine.IsPaidStage(order.Status))
        {
            return ServiceResult<OrderDto>.Validation(
                "Оплаченный заказ отменяется через поддержку: нужно рассчитать возврат.");
        }

        order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        var moved = Move(order, OrderStatuses.Cancelled, userId, null, order.CancellationReason);
        if (moved is not null)
        {
            return moved;
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> AcceptEstimateAsync(
        long userId, long orderId, int version, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        var estimate = order.Estimates.FirstOrDefault(e => e.Version == version);
        if (estimate is null)
        {
            return ServiceResult<OrderDto>.NotFound("Версия сметы не найдена.");
        }

        if (estimate.Status != EstimateStatuses.Published)
        {
            // Catches the race that matters: the client had the page open while the dispatcher
            // published a correction, and is now agreeing to lines that are no longer offered.
            return ServiceResult<OrderDto>.Validation(
                "Эта версия сметы больше не действует. Обновите страницу и посмотрите актуальную.");
        }

        if (estimate.ValidUntil is not null && estimate.ValidUntil < DateTimeOffset.UtcNow)
        {
            return ServiceResult<OrderDto>.Validation("Срок действия сметы истёк. Мы подготовим новую.");
        }

        estimate.Status = EstimateStatuses.Accepted;
        estimate.AcceptedAt = DateTimeOffset.UtcNow;

        var moved = Move(order, OrderStatuses.AwaitingPayment, userId, null, $"смета v{version}");
        if (moved is not null)
        {
            return moved;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Estimate v{Version} accepted on order {Number}", version, order.Number);

        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> RejectEstimateAsync(
        long userId, long orderId, int version, string? reason, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        var estimate = order.Estimates.FirstOrDefault(e => e.Version == version);
        if (estimate is null || estimate.Status != EstimateStatuses.Published)
        {
            return ServiceResult<OrderDto>.Validation("Эта версия сметы больше не действует.");
        }

        estimate.Status = EstimateStatuses.Rejected;
        estimate.RejectedAt = DateTimeOffset.UtcNow;
        estimate.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        // The order stays where it is rather than closing: a rejection is usually "not at this
        // price", and the dispatcher's next move is another version, not a lost customer.
        await _db.SaveChangesAsync(ct);

        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> TransitionAsync(
        long orderId, string toStatus, long? actorUserId, string? actorRole, string? reason,
        CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        var moved = Move(order, toStatus, actorUserId, actorRole, reason);
        if (moved is not null)
        {
            return moved;
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> AcceptWorkAsync(
        long userId, long orderId, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        if (order.Status != OrderStatuses.CustomerReview)
        {
            return ServiceResult<OrderDto>.Validation("Принять можно только выполненную работу.");
        }

        var failed = Move(order, OrderStatuses.Completed, userId, null, "customer_accepted");
        if (failed is not null) return failed;

        await _db.SaveChangesAsync(ct);
        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> DisputeAsync(
        long userId, long orderId, string? reason, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null || order.CustomerUserId != userId)
        {
            return ServiceResult<OrderDto>.NotFound("Заказ не найден.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            // A dispute with no statement of what is wrong cannot be resolved, only argued about.
            return ServiceResult<OrderDto>.Validation("Опишите, что не так — без этого спор не разобрать.");
        }

        var failed = Move(order, OrderStatuses.Disputed, userId, null, reason.Trim());
        if (failed is not null) return failed;

        await _db.SaveChangesAsync(ct);
        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    // ---------------------------------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The only place <c>order.Status</c> is written. Returns null on success, or the failure to
    /// hand back — an unusual shape, chosen so no caller can move an order and forget to check.
    /// </summary>
    private ServiceResult<OrderDto>? Move(Order order, string to, long? actorUserId, string? actorRole, string? reason)
    {
        if (order.Status == to)
        {
            return null;
        }

        if (!OrderStateMachine.CanTransition(order.Status, to))
        {
            return ServiceResult<OrderDto>.Validation(
                $"Нельзя перейти из «{OrderStatusPresentation.For(order.Status).Label}» " +
                $"в «{OrderStatusPresentation.For(to).Label}».");
        }

        var from = order.Status;
        order.Status = to;

        if (to == OrderStatuses.Paid) order.PaidAt = DateTimeOffset.UtcNow;
        if (to == OrderStatuses.Completed) order.CompletedAt = DateTimeOffset.UtcNow;

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = from,
            ToStatus = to,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Reason = reason,
        });

        _logger.LogInformation("Order {Number}: {From} → {To}", order.Number, from, to);
        return null;
    }

    private Task<Order?> LoadAsync(long orderId, CancellationToken ct) =>
        _db.Orders
            .Include(o => o.BurialSite)
            .Include(o => o.Estimates).ThenInclude(e => e.Lines)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    /// <summary>
    /// "ПР-260826-0007". Date plus a daily counter, because the number is read aloud in support
    /// calls and a raw id tells nobody anything.
    /// </summary>
    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var today = DateTimeOffset.UtcNow;
        var prefix = $"ПР-{today:yyMMdd}-";

        var todayCount = await _db.Orders.CountAsync(o => o.Number.StartsWith(prefix), ct);
        return $"{prefix}{todayCount + 1:D4}";
    }

    private static OrderSummaryDto MapSummary(Order o)
    {
        var state = OrderStatusPresentation.For(o.Status);
        return new OrderSummaryDto
        {
            QueueGroup = OrderStatuses.QueueGroup(o.Status),
            Id = o.Id,
            Number = o.Number,
            Status = o.Status,
            StatusLabel = state.Label,
            Cta = state.Cta,
            PackageTitle = o.PackageTitle,
            DeceasedFullName = o.BurialSite?.DeceasedFullName ?? string.Empty,
            PreferredFrom = o.PreferredFrom,
            PreferredTo = o.PreferredTo,
            CreatedAt = o.CreatedAt,
        };
    }

    private static OrderDto MapOrder(Order o)
    {
        var state = OrderStatusPresentation.For(o.Status);

        // Only what the client may see: the published version awaiting them, plus whatever they
        // already accepted or rejected. Drafts are the dispatcher's workings.
        var visible = o.Estimates
            .Where(e => e.Status != EstimateStatuses.Draft)
            .OrderByDescending(e => e.Version)
            .Select(MapEstimate)
            .ToList();

        return new OrderDto
        {
            Id = o.Id,
            Number = o.Number,
            Status = o.Status,
            StatusLabel = state.Label,
            Cta = state.Cta,
            BurialSiteId = o.BurialSiteId,
            DeceasedFullName = o.BurialSite?.DeceasedFullName ?? string.Empty,
            PackageCode = o.PackageCode,
            PackageVersion = o.PackageVersion,
            PackageTitle = o.PackageTitle,
            PackagePriceFromRub = o.PackagePriceFromRub,
            WarrantyDays = o.WarrantyDays,
            WarrantyUntil = o.WarrantyUntil,
            PreferredFrom = o.PreferredFrom,
            PreferredTo = o.PreferredTo,
            Comment = o.CustomerComment,
            CancellationReason = o.CancellationReason,
            SubmittedAt = o.SubmittedAt,
            PaidAt = o.PaidAt,
            CompletedAt = o.CompletedAt,
            CreatedAt = o.CreatedAt,
            Estimates = visible,
            History = o.StatusHistory
                .OrderBy(h => h.CreatedAt)
                .Select(h => new OrderHistoryDto
                {
                    ToStatus = h.ToStatus,
                    Label = OrderStatusPresentation.For(h.ToStatus).Label,
                    Reason = h.Reason,
                    CreatedAt = h.CreatedAt,
                })
                .ToList(),
        };
    }

    internal static EstimateDto MapEstimate(Estimate e) => new()
    {
        Version = e.Version,
        Status = e.Status,
        TotalRub = e.TotalRub,
        ValidUntil = e.ValidUntil,
        Note = e.Note,
        PublishedAt = e.PublishedAt,
        AcceptedAt = e.AcceptedAt,
        RejectedAt = e.RejectedAt,
        Lines = e.Lines
            .OrderBy(l => l.SortOrder)
            .Select(l => new EstimateLineDto
            {
                Type = l.Type,
                Title = l.Title,
                Quantity = l.Quantity,
                Unit = l.Unit,
                UnitPriceRub = l.UnitPriceRub,
                TotalRub = l.TotalRub,
            })
            .ToList(),
    };
}
