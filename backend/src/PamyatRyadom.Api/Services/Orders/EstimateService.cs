using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Orders;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Services.Orders;

public interface IEstimateService
{
    Task<ServiceResult<IReadOnlyList<OrderSummaryDto>>> QueueAsync(string? status, CancellationToken ct = default);
    Task<ServiceResult<OrderDto>> GetAsync(long orderId, CancellationToken ct = default);

    /// <summary>Creates the next draft version for an order. Drafts are the dispatcher's
    /// workings and are never visible to the client.</summary>
    Task<ServiceResult<EstimateDto>> CreateDraftAsync(long actorId, long orderId, SaveEstimateDto dto, CancellationToken ct = default);

    Task<ServiceResult<EstimateDto>> UpdateDraftAsync(long actorId, long orderId, int version, SaveEstimateDto dto, CancellationToken ct = default);

    /// <summary>Publishes a draft to the client. Supersedes whatever was awaiting them, which
    /// invalidates any acceptance that had not yet been paid (BR-002).</summary>
    Task<ServiceResult<OrderDto>> PublishAsync(long actorId, long orderId, int version, CancellationToken ct = default);
}

public sealed class EstimateService : IEstimateService
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly ILogger<EstimateService> _logger;

    public EstimateService(AppDbContext db, IOrderService orders, ILogger<EstimateService> logger)
    {
        _db = db;
        _orders = orders;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<OrderSummaryDto>>> QueueAsync(
        string? status, CancellationToken ct = default)
    {
        var query = _db.Orders.AsNoTracking().Include(o => o.BurialSite).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }
        else
        {
            // Everything still alive, in one call. Splitting it into "ours" and "theirs" is the
            // caller's job (OrderStatuses.QueueGroup): an order waiting on a client is not work,
            // but it is exactly the thing that quietly rots if nobody ever sees it.
            var active = OrderStatuses.NeedsStaffAction
                .Concat(OrderStatuses.WaitingOnCustomer)
                .Concat(OrderStatuses.InFlight)
                .ToArray();
            query = query.Where(o => active.Contains(o.Status));
        }

        var orders = await query.OrderBy(o => o.CreatedAt).ToListAsync(ct);

        return ServiceResult<IReadOnlyList<OrderSummaryDto>>.Ok(orders
            .Select(o => new OrderSummaryDto
            {
                QueueGroup = OrderStatuses.QueueGroup(o.Status),
                Id = o.Id,
                Number = o.Number,
                Status = o.Status,
                StatusLabel = OrderStatusPresentation.For(o.Status).Label,
                PackageTitle = o.PackageTitle,
                DeceasedFullName = o.BurialSite?.DeceasedFullName ?? string.Empty,
                PreferredFrom = o.PreferredFrom,
                PreferredTo = o.PreferredTo,
                CreatedAt = o.CreatedAt,
            })
            .ToList());
    }

    public async Task<ServiceResult<OrderDto>> GetAsync(long orderId, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        return order is null
            ? ServiceResult<OrderDto>.NotFound("Заказ не найден.")
            : ServiceResult<OrderDto>.Ok(MapForStaff(order));
    }

    public async Task<ServiceResult<EstimateDto>> CreateDraftAsync(
        long actorId, long orderId, SaveEstimateDto dto, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null)
        {
            return ServiceResult<EstimateDto>.NotFound("Заказ не найден.");
        }

        if (OrderStateMachine.IsPaidStage(order.Status))
        {
            // Re-pricing paid work is an extra, agreed and charged separately (BR-004) — not a
            // new estimate quietly replacing what was already settled.
            return ServiceResult<EstimateDto>.Validation(
                "Заказ уже оплачен. Дополнительные работы оформляются отдельным согласованием.");
        }

        var invalid = ValidateLines(dto);
        if (invalid is not null)
        {
            return invalid;
        }

        var nextVersion = order.Estimates.Count == 0 ? 1 : order.Estimates.Max(e => e.Version) + 1;

        var estimate = new Estimate
        {
            OrderId = order.Id,
            Version = nextVersion,
            Status = EstimateStatuses.Draft,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            ValidUntil = dto.ValidUntil,
        };

        ApplyLines(estimate, dto);
        estimate.TotalRub = Total(estimate);

        _db.Estimates.Add(estimate);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Estimate draft v{Version} created on order {Number} by user {ActorId}",
            nextVersion, order.Number, actorId);

        return ServiceResult<EstimateDto>.Created(OrderService.MapEstimate(estimate));
    }

    public async Task<ServiceResult<EstimateDto>> UpdateDraftAsync(
        long actorId, long orderId, int version, SaveEstimateDto dto, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        var estimate = order?.Estimates.FirstOrDefault(e => e.Version == version);

        if (order is null || estimate is null)
        {
            return ServiceResult<EstimateDto>.NotFound("Версия сметы не найдена.");
        }

        if (estimate.Status != EstimateStatuses.Draft)
        {
            // Published means shown to a client. Editing it in place would change what they are
            // looking at while they look at it.
            return ServiceResult<EstimateDto>.Validation(
                "Опубликованную смету изменить нельзя. Создайте новую версию.");
        }

        var invalid = ValidateLines(dto);
        if (invalid is not null)
        {
            return invalid;
        }

        estimate.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        estimate.ValidUntil = dto.ValidUntil;

        _db.EstimateLines.RemoveRange(estimate.Lines);
        estimate.Lines.Clear();
        ApplyLines(estimate, dto);
        estimate.TotalRub = Total(estimate);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<EstimateDto>.Ok(OrderService.MapEstimate(estimate));
    }

    public async Task<ServiceResult<OrderDto>> PublishAsync(
        long actorId, long orderId, int version, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        var estimate = order?.Estimates.FirstOrDefault(e => e.Version == version);

        if (order is null || estimate is null)
        {
            return ServiceResult<OrderDto>.NotFound("Версия сметы не найдена.");
        }

        if (estimate.Status != EstimateStatuses.Draft)
        {
            return ServiceResult<OrderDto>.Validation("Эта версия уже опубликована или закрыта.");
        }

        if (estimate.Lines.Count == 0)
        {
            return ServiceResult<OrderDto>.Validation("Нельзя отправить клиенту пустую смету.");
        }

        // Anything the client was still looking at is superseded. This is what enforces BR-002:
        // a correction invalidates the previous version rather than sitting beside it, so there
        // is never a question of which quote was agreed to.
        foreach (var other in order.Estimates.Where(e =>
                     e.Version != version && e.Status is EstimateStatuses.Published or EstimateStatuses.Accepted))
        {
            other.Status = EstimateStatuses.Superseded;
        }

        estimate.Status = EstimateStatuses.Published;
        estimate.PublishedAt = DateTimeOffset.UtcNow;
        estimate.PublishedByUserId = actorId;
        estimate.TotalRub = Total(estimate);

        await _db.SaveChangesAsync(ct);

        // Moves the order to "estimate ready" — including from awaiting_payment, which is exactly
        // the correction case: the client had accepted, we found something, they must agree again.
        var moved = await _orders.TransitionAsync(
            order.Id, OrderStatuses.EstimateReady, actorId, "dispatcher", $"смета v{version}", ct);

        if (!moved.Succeeded)
        {
            return moved;
        }

        _logger.LogInformation("Estimate v{Version} published on order {Number}", version, order.Number);

        var reloaded = await LoadAsync(orderId, ct);
        return ServiceResult<OrderDto>.Ok(MapForStaff(reloaded!));
    }

    // ---------------------------------------------------------------------------------------------

    private static ServiceResult<EstimateDto>? ValidateLines(SaveEstimateDto dto)
    {
        var lines = dto.Lines ?? new List<SaveEstimateLineDto>();

        if (lines.Count == 0)
        {
            return ServiceResult<EstimateDto>.Validation("Смета без строк не имеет смысла.");
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Title))
                return ServiceResult<EstimateDto>.Validation("У каждой строки должно быть название.");

            if (!EstimateLineTypes.All.Contains(line.Type))
                return ServiceResult<EstimateDto>.Validation($"Неизвестный тип строки «{line.Type}».");

            if (line.Quantity <= 0)
                return ServiceResult<EstimateDto>.Validation($"«{line.Title}»: количество должно быть больше нуля.");

            // Only a discount may pull the total down; anything else negative is a data error
            // that would quietly reduce what the client owes.
            if (line.Type == EstimateLineTypes.Discount && line.UnitPriceRub > 0)
                return ServiceResult<EstimateDto>.Validation($"«{line.Title}»: скидка задаётся отрицательной суммой.");

            if (line.Type != EstimateLineTypes.Discount && line.UnitPriceRub < 0)
                return ServiceResult<EstimateDto>.Validation($"«{line.Title}»: отрицательная цена допустима только для скидки.");
        }

        var total = lines.Sum(l => decimal.Round(l.Quantity * l.UnitPriceRub, 2));
        if (total < 0)
        {
            // BR-003. A discount larger than the work it discounts means we owe the client money
            // for the privilege of doing the job.
            return ServiceResult<EstimateDto>.Validation("Итог сметы не может быть отрицательным.");
        }

        return null;
    }

    private static void ApplyLines(Estimate estimate, SaveEstimateDto dto)
    {
        var order = 0;
        foreach (var line in dto.Lines ?? new List<SaveEstimateLineDto>())
        {
            estimate.Lines.Add(new EstimateLine
            {
                Type = line.Type,
                Title = line.Title.Trim(),
                Quantity = line.Quantity,
                Unit = string.IsNullOrWhiteSpace(line.Unit) ? null : line.Unit.Trim(),
                UnitPriceRub = line.UnitPriceRub,
                SortOrder = order++,
            });
        }
    }

    private static decimal Total(Estimate estimate) =>
        decimal.Round(estimate.Lines.Sum(l => l.Quantity * l.UnitPriceRub), 2);

    private Task<Order?> LoadAsync(long orderId, CancellationToken ct) =>
        _db.Orders
            .Include(o => o.BurialSite)
            .Include(o => o.Estimates).ThenInclude(e => e.Lines)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    /// <summary>Staff view: unlike the client's, it includes draft versions, which are the
    /// dispatcher's own workings.</summary>
    private static OrderDto MapForStaff(Order o)
    {
        var state = OrderStatusPresentation.For(o.Status);

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
            Estimates = o.Estimates.OrderByDescending(e => e.Version).Select(OrderService.MapEstimate).ToList(),
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
}
