using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Dispatch;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Dispatch;
using PamyatRyadom.Api.Models.Media;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Orders;

namespace PamyatRyadom.Api.Services.Dispatch;

public interface IVisitService
{
    Task<ServiceResult<VisitDto>> AssignAsync(long actorId, long orderId, AssignVisitDto dto, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<VisitDto>>> ListForExecutorAsync(long executorId, CancellationToken ct = default);
    Task<ServiceResult<VisitDto>> GetForExecutorAsync(long executorId, long visitId, CancellationToken ct = default);
    Task<ServiceResult<VisitDto>> AcceptAsync(long executorId, long visitId, CancellationToken ct = default);
    Task<ServiceResult<VisitDto>> DeclineAsync(long executorId, long visitId, string? reason, CancellationToken ct = default);
    Task<ServiceResult<VisitDto>> StartAsync(long executorId, long visitId, CancellationToken ct = default);
    Task<ServiceResult<VisitDto>> SubmitReportAsync(long executorId, long visitId, SubmitReportDto dto, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<VisitDto>>> QaQueueAsync(CancellationToken ct = default);
    Task<ServiceResult<VisitDto>> ApproveAsync(long actorId, long visitId, CancellationToken ct = default);
    Task<ServiceResult<VisitDto>> SendBackAsync(long actorId, long visitId, string? note, CancellationToken ct = default);

    /// <summary>The report as the client may see it — only after QA approved it (BR-010).</summary>
    Task<ServiceResult<VisitReportDto>> GetReportForClientAsync(long userId, long orderId, CancellationToken ct = default);

    /// <summary>Active executors, for the dispatcher's assignment picker.</summary>
    Task<ServiceResult<IReadOnlyList<ExecutorOptionDto>>> ListExecutorsAsync(CancellationToken ct = default);

    /// <summary>Visits on one order, for the staff order card.</summary>
    Task<ServiceResult<IReadOnlyList<VisitDto>>> ListForOrderAsync(long orderId, CancellationToken ct = default);
}

/// <summary>
/// Dispatching a paid order to an executor, and getting a photo report back.
///
/// Two invariants run through everything below. A client never sees a report QA has not approved,
/// because an unreviewed report is a claim rather than evidence. And the executor's payout never
/// leaves this module in anything a client can read.
/// </summary>
public sealed class VisitService : IVisitService
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly ILogger<VisitService> _logger;

    /// <summary>The minimum a report must carry before QA will even look: the place as found and
    /// the place as left. Without both, "compare these two" is not on offer (BR-008).</summary>
    private const int MinBeforePhotos = 1;
    private const int MinAfterPhotos = 1;

    public VisitService(AppDbContext db, IOrderService orders, ILogger<VisitService> logger)
    {
        _db = db;
        _orders = orders;
        _logger = logger;
    }

    public async Task<ServiceResult<VisitDto>> AssignAsync(
        long actorId, long orderId, AssignVisitDto dto, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
        {
            return ServiceResult<VisitDto>.NotFound("Заказ не найден.");
        }

        if (order.Status is not (OrderStatuses.Paid or OrderStatuses.Assigning))
        {
            return ServiceResult<VisitDto>.Validation(
                "Назначить исполнителя можно только по оплаченному заказу.");
        }

        var executor = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.ExecutorUserId, ct);
        if (executor is null || executor.Role != Models.Auth.UserRoles.Executor)
        {
            return ServiceResult<VisitDto>.Validation("Такого исполнителя нет.");
        }

        if (executor.Status != Models.Auth.UserStatuses.Active)
        {
            return ServiceResult<VisitDto>.Validation("Исполнитель заблокирован.");
        }

        // One live visit per order. A second offer out at the same time means two people can turn
        // up at one grave, and the client is billed once.
        var live = await _db.Visits.AnyAsync(
            v => v.OrderId == orderId &&
                 (v.Status == VisitStatuses.Offered || v.Status == VisitStatuses.Accepted ||
                  v.Status == VisitStatuses.InProgress || v.Status == VisitStatuses.Submitted ||
                  v.Status == VisitStatuses.Rework),
            ct);

        if (live)
        {
            return ServiceResult<VisitDto>.Validation("По заказу уже есть активный визит.");
        }

        var visit = new Visit
        {
            OrderId = orderId,
            ExecutorUserId = dto.ExecutorUserId,
            Status = VisitStatuses.Offered,
            ScheduledFor = dto.ScheduledFor,
            OfferExpiresAt = DateTimeOffset.UtcNow.AddHours(Math.Clamp(dto.OfferHours, 1, 168)),
            PayoutRub = dto.PayoutRub,
        };

        foreach (var item in await ChecklistForAsync(order.ServicePackageId, ct))
        {
            visit.ChecklistItems.Add(item);
        }

        _db.Visits.Add(visit);
        await _db.SaveChangesAsync(ct);

        if (order.Status == OrderStatuses.Paid)
        {
            var moved = await _orders.TransitionAsync(orderId, OrderStatuses.Assigning, actorId, null, "dispatch", ct);
            if (!moved.Succeeded) return Fail(moved);
        }

        var assigned = await _orders.TransitionAsync(orderId, OrderStatuses.Assigned, actorId, null, "offer_sent", ct);
        if (!assigned.Succeeded) return Fail(assigned);

        _logger.LogInformation("Visit {VisitId} offered to executor {ExecutorId} for order {OrderId}",
            visit.Id, dto.ExecutorUserId, orderId);

        return ServiceResult<VisitDto>.Created(await MapAsync(visit.Id, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<VisitDto>>> ListForExecutorAsync(
        long executorId, CancellationToken ct = default)
    {
        var ids = await _db.Visits
            .Where(v => v.ExecutorUserId == executorId)
            .OrderByDescending(v => v.Id)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var visits = new List<VisitDto>(ids.Count);
        foreach (var id in ids)
        {
            visits.Add(await MapAsync(id, ct));
        }

        return ServiceResult<IReadOnlyList<VisitDto>>.Ok(visits);
    }

    public async Task<ServiceResult<VisitDto>> GetForExecutorAsync(
        long executorId, long visitId, CancellationToken ct = default)
    {
        var owned = await _db.Visits.AnyAsync(v => v.Id == visitId && v.ExecutorUserId == executorId, ct);
        return owned
            ? ServiceResult<VisitDto>.Ok(await MapAsync(visitId, ct))
            : ServiceResult<VisitDto>.NotFound("Визит не найден.");
    }

    public async Task<ServiceResult<VisitDto>> AcceptAsync(
        long executorId, long visitId, CancellationToken ct = default)
    {
        var visit = await LoadForExecutorAsync(executorId, visitId, ct);
        if (visit is null) return ServiceResult<VisitDto>.NotFound("Визит не найден.");

        if (visit.Status != VisitStatuses.Offered)
        {
            return ServiceResult<VisitDto>.Validation("Этот визит уже не в статусе предложения.");
        }

        if (visit.OfferExpiresAt is { } expiry && expiry < DateTimeOffset.UtcNow)
        {
            // An expired offer is not accepted late. It goes back to dispatch, who may re-offer
            // it to the same person — that is a decision, not a default.
            visit.Status = VisitStatuses.Declined;
            visit.DeclineReason = "offer_expired";
            visit.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _orders.TransitionAsync(visit.OrderId, OrderStatuses.Assigning, null, null, "offer_expired", ct);

            return ServiceResult<VisitDto>.Validation("Срок предложения истёк — заказ вернулся диспетчеру.");
        }

        visit.Status = VisitStatuses.Accepted;
        visit.AcceptedAt = DateTimeOffset.UtcNow;
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<VisitDto>.Ok(await MapAsync(visitId, ct));
    }

    public async Task<ServiceResult<VisitDto>> DeclineAsync(
        long executorId, long visitId, string? reason, CancellationToken ct = default)
    {
        var visit = await LoadForExecutorAsync(executorId, visitId, ct);
        if (visit is null) return ServiceResult<VisitDto>.NotFound("Визит не найден.");

        if (visit.Status is not (VisitStatuses.Offered or VisitStatuses.Accepted))
        {
            return ServiceResult<VisitDto>.Validation("Отказаться можно только до начала работ.");
        }

        visit.Status = VisitStatuses.Declined;
        visit.DeclineReason = reason;
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var moved = await _orders.TransitionAsync(
            visit.OrderId, OrderStatuses.Assigning, executorId, null, "executor_declined", ct);
        if (!moved.Succeeded) return Fail(moved);

        return ServiceResult<VisitDto>.Ok(await MapAsync(visitId, ct));
    }

    public async Task<ServiceResult<VisitDto>> StartAsync(
        long executorId, long visitId, CancellationToken ct = default)
    {
        var visit = await LoadForExecutorAsync(executorId, visitId, ct);
        if (visit is null) return ServiceResult<VisitDto>.NotFound("Визит не найден.");

        if (visit.Status != VisitStatuses.Accepted)
        {
            return ServiceResult<VisitDto>.Validation("Начать можно только принятый визит.");
        }

        visit.Status = VisitStatuses.InProgress;
        visit.StartedAt = DateTimeOffset.UtcNow;
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var moved = await _orders.TransitionAsync(
            visit.OrderId, OrderStatuses.InProgress, executorId, null, "visit_started", ct);
        if (!moved.Succeeded) return Fail(moved);

        return ServiceResult<VisitDto>.Ok(await MapAsync(visitId, ct));
    }

    public async Task<ServiceResult<VisitDto>> SubmitReportAsync(
        long executorId, long visitId, SubmitReportDto dto, CancellationToken ct = default)
    {
        var visit = await LoadForExecutorAsync(executorId, visitId, ct);
        if (visit is null) return ServiceResult<VisitDto>.NotFound("Визит не найден.");

        if (visit.Status is not (VisitStatuses.InProgress or VisitStatuses.Rework))
        {
            return ServiceResult<VisitDto>.Validation("Отчёт подаётся по визиту в работе.");
        }

        var answers = dto.Checklist.ToDictionary(a => a.Key, a => a, StringComparer.Ordinal);

        foreach (var item in visit.ChecklistItems)
        {
            if (!answers.TryGetValue(item.Key, out var answer))
            {
                return ServiceResult<VisitDto>.Validation(
                    $"По пункту «{item.Title}» нет ответа. Отметьте каждый пункт.");
            }

            if (!ChecklistResults.All.Contains(answer.Result) || answer.Result == ChecklistResults.Pending)
            {
                return ServiceResult<VisitDto>.Validation($"Недопустимый ответ по пункту «{item.Title}».");
            }

            if (ChecklistResults.NeedsNote(answer.Result) && string.IsNullOrWhiteSpace(answer.Note))
            {
                return ServiceResult<VisitDto>.Validation(
                    $"По пункту «{item.Title}» нужен комментарий — почему не сделано.");
            }

            item.Result = answer.Result;
            item.Note = answer.Note?.Trim();
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // The photographs are the deliverable. A report without a before and an after is not a
        // report, and letting it through to QA only moves the rejection one step later.
        var photos = await _db.MediaAssets
            .Where(m => m.OwnerType == MediaOwnerTypes.Visit &&
                        m.OwnerId == visitId &&
                        m.Status == MediaStatuses.Ready)
            .GroupBy(m => m.Phase)
            .Select(g => new { Phase = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var before = photos.FirstOrDefault(p => p.Phase == MediaPhases.Before)?.Count ?? 0;
        var after = photos.FirstOrDefault(p => p.Phase == MediaPhases.After)?.Count ?? 0;

        if (before < MinBeforePhotos || after < MinAfterPhotos)
        {
            return ServiceResult<VisitDto>.Validation(
                "Нужны фотографии «до» и «после» — по ним клиент видит результат.",
                new { before, after, requiredBefore = MinBeforePhotos, requiredAfter = MinAfterPhotos });
        }

        visit.Status = VisitStatuses.Submitted;
        visit.SubmittedAt = DateTimeOffset.UtcNow;
        visit.ExecutorNote = dto.Note?.Trim();
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var moved = await _orders.TransitionAsync(
            visit.OrderId, OrderStatuses.QaReview, executorId, null, "report_submitted", ct);
        if (!moved.Succeeded) return Fail(moved);

        return ServiceResult<VisitDto>.Ok(await MapAsync(visitId, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<VisitDto>>> QaQueueAsync(CancellationToken ct = default)
    {
        var ids = await _db.Visits
            .Where(v => v.Status == VisitStatuses.Submitted)
            .OrderBy(v => v.SubmittedAt)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var visits = new List<VisitDto>(ids.Count);
        foreach (var id in ids)
        {
            visits.Add(await MapAsync(id, ct));
        }

        return ServiceResult<IReadOnlyList<VisitDto>>.Ok(visits);
    }

    public async Task<ServiceResult<VisitDto>> ApproveAsync(
        long actorId, long visitId, CancellationToken ct = default)
    {
        var visit = await _db.Visits.Include(v => v.ChecklistItems)
            .FirstOrDefaultAsync(v => v.Id == visitId, ct);

        if (visit is null) return ServiceResult<VisitDto>.NotFound("Визит не найден.");

        if (visit.Status != VisitStatuses.Submitted)
        {
            return ServiceResult<VisitDto>.Validation("Принять можно только поданный отчёт.");
        }

        visit.Status = VisitStatuses.Approved;
        visit.ReviewedAt = DateTimeOffset.UtcNow;
        visit.ReviewedByUserId = actorId;
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var moved = await _orders.TransitionAsync(
            visit.OrderId, OrderStatuses.CustomerReview, actorId, null, "qa_approved", ct);
        if (!moved.Succeeded) return Fail(moved);

        return ServiceResult<VisitDto>.Ok(await MapAsync(visitId, ct));
    }

    public async Task<ServiceResult<VisitDto>> SendBackAsync(
        long actorId, long visitId, string? note, CancellationToken ct = default)
    {
        var visit = await _db.Visits.FirstOrDefaultAsync(v => v.Id == visitId, ct);
        if (visit is null) return ServiceResult<VisitDto>.NotFound("Визит не найден.");

        if (visit.Status != VisitStatuses.Submitted)
        {
            return ServiceResult<VisitDto>.Validation("Вернуть можно только поданный отчёт.");
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            // A rejection with no reason is not review, it is an obstacle: the executor has to
            // guess what to redo, and will guess wrong.
            return ServiceResult<VisitDto>.Validation("Укажите, что именно нужно переделать.");
        }

        visit.Status = VisitStatuses.Rework;
        visit.ReviewNote = note.Trim();
        visit.ReviewedAt = DateTimeOffset.UtcNow;
        visit.ReviewedByUserId = actorId;
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var moved = await _orders.TransitionAsync(
            visit.OrderId, OrderStatuses.InProgress, actorId, null, "qa_rework", ct);
        if (!moved.Succeeded) return Fail(moved);

        return ServiceResult<VisitDto>.Ok(await MapAsync(visitId, ct));
    }

    public async Task<ServiceResult<VisitReportDto>> GetReportForClientAsync(
        long userId, long orderId, CancellationToken ct = default)
    {
        var owns = await _db.Orders.AnyAsync(o => o.Id == orderId && o.CustomerUserId == userId, ct);
        if (!owns) return ServiceResult<VisitReportDto>.NotFound("Заказ не найден.");

        // Approved only. A report QA has not passed is a claim, not evidence, and showing it
        // early is how a client ends up disputing something we would have caught ourselves.
        var visit = await _db.Visits
            .AsNoTracking()
            .Include(v => v.ChecklistItems)
            .Where(v => v.OrderId == orderId && v.Status == VisitStatuses.Approved)
            .OrderByDescending(v => v.ReviewedAt)
            .FirstOrDefaultAsync(ct);

        if (visit is null)
        {
            return ServiceResult<VisitReportDto>.NotFound("Отчёт ещё не готов.");
        }

        return ServiceResult<VisitReportDto>.Ok(new VisitReportDto
        {
            Id = visit.Id,
            Status = visit.Status,
            StatusLabel = LabelFor(visit.Status),
            VisitedOn = visit.StartedAt ?? visit.SubmittedAt,
            Note = visit.ExecutorNote,
            Checklist = visit.ChecklistItems.OrderBy(i => i.SortOrder).Select(MapItem).ToList(),
        });
    }

    public async Task<ServiceResult<IReadOnlyList<ExecutorOptionDto>>> ListExecutorsAsync(
        CancellationToken ct = default)
    {
        var executors = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == Models.Auth.UserRoles.Executor &&
                        u.Status == Models.Auth.UserStatuses.Active)
            .OrderBy(u => u.DisplayName)
            .Select(u => new ExecutorOptionDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName ?? $"Исполнитель #{u.Id}",
            })
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<ExecutorOptionDto>>.Ok(executors);
    }

    public async Task<ServiceResult<IReadOnlyList<VisitDto>>> ListForOrderAsync(
        long orderId, CancellationToken ct = default)
    {
        var ids = await _db.Visits
            .Where(v => v.OrderId == orderId)
            .OrderByDescending(v => v.Id)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var visits = new List<VisitDto>(ids.Count);
        foreach (var id in ids)
        {
            visits.Add(await MapAsync(id, ct));
        }

        return ServiceResult<IReadOnlyList<VisitDto>>.Ok(visits);
    }

    // ---------------------------------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------------------------------

    private Task<Visit?> LoadForExecutorAsync(long executorId, long visitId, CancellationToken ct) =>
        _db.Visits
            .Include(v => v.ChecklistItems)
            .FirstOrDefaultAsync(v => v.Id == visitId && v.ExecutorUserId == executorId, ct);

    /// <summary>Copies the package's checklist onto a new visit. An empty template is legitimate —
    /// an inspection has nothing to tick — so this returns an empty list rather than failing.</summary>
    private async Task<List<VisitChecklistItem>> ChecklistForAsync(long packageId, CancellationToken ct)
    {
        var template = await _db.ChecklistTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ServicePackageId == packageId, ct);

        if (template?.Items is null)
        {
            return new List<VisitChecklistItem>();
        }

        var items = template.Items.Deserialize<List<ChecklistItem>>(ChecklistJson.Options)
                    ?? new List<ChecklistItem>();

        return items
            // A blank key would land as a not-null violation on save. Skipping is wrong data
            // handled quietly; the log is what makes it findable.
            .Where(item =>
            {
                if (!string.IsNullOrWhiteSpace(item.Key)) return true;
                _logger.LogError("Checklist template for package {PackageId} has an item with no key", packageId);
                return false;
            })
            .Select((item, index) => new VisitChecklistItem
        {
            Key = item.Key,
            Title = item.Title,
            Optional = item.Optional,
            Result = ChecklistResults.Pending,
            SortOrder = index,
        }).ToList();
    }

    private static ServiceResult<VisitDto> Fail(ServiceResult<Dtos.Orders.OrderDto> moved) =>
        ServiceResult<VisitDto>.Fail(moved.StatusCode, moved.Errors.ToArray());

    private async Task<VisitDto> MapAsync(long visitId, CancellationToken ct)
    {
        var row = await _db.Visits
            .AsNoTracking()
            .Include(v => v.ChecklistItems)
            .Include(v => v.Order!).ThenInclude(o => o.BurialSite!).ThenInclude(s => s.Cemetery)
            .FirstAsync(v => v.Id == visitId, ct);

        var executorName = row.ExecutorUserId is null
            ? null
            : await _db.Users.Where(u => u.Id == row.ExecutorUserId)
                .Select(u => u.DisplayName).FirstOrDefaultAsync(ct);

        return new VisitDto
        {
            Id = row.Id,
            OrderId = row.OrderId,
            OrderNumber = row.Order?.Number ?? string.Empty,
            Status = row.Status,
            StatusLabel = LabelFor(row.Status),
            ExecutorUserId = row.ExecutorUserId,
            ExecutorName = executorName,
            ScheduledFor = row.ScheduledFor,
            OfferExpiresAt = row.OfferExpiresAt,
            SubmittedAt = row.SubmittedAt,
            ReviewedAt = row.ReviewedAt,
            ExecutorNote = row.ExecutorNote,
            ReviewNote = row.ReviewNote,
            DeclineReason = row.DeclineReason,
            PayoutRub = row.PayoutRub,
            DeceasedFullName = row.Order?.BurialSite?.DeceasedFullName ?? string.Empty,
            CemeteryName = row.Order?.BurialSite?.Cemetery?.Name,
            PlotSection = row.Order?.BurialSite?.PlotSection,
            Landmarks = row.Order?.BurialSite?.Landmarks,
            Checklist = row.ChecklistItems.OrderBy(i => i.SortOrder).Select(MapItem).ToList(),
        };
    }

    private static ChecklistItemDto MapItem(VisitChecklistItem i) => new()
    {
        Key = i.Key,
        Title = i.Title,
        Optional = i.Optional,
        Result = i.Result,
        ResultLabel = i.Result switch
        {
            ChecklistResults.Done => "Сделано",
            ChecklistResults.Impossible => "Не удалось",
            ChecklistResults.NotRequired => "Не потребовалось",
            _ => "Ожидает",
        },
        Note = i.Note,
        SortOrder = i.SortOrder,
    };

    private static string LabelFor(string status) => status switch
    {
        VisitStatuses.Offered => "Предложен исполнителю",
        VisitStatuses.Accepted => "Принят исполнителем",
        VisitStatuses.Declined => "Отклонён",
        VisitStatuses.InProgress => "В работе",
        VisitStatuses.Submitted => "Отчёт на проверке",
        VisitStatuses.Rework => "Отправлен на доработку",
        VisitStatuses.Approved => "Проверен",
        VisitStatuses.Failed => "Не выполнен",
        _ => status,
    };
}
