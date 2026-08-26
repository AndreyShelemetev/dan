using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Catalog;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Services.Catalog;

public interface ICatalogAdminService
{
    Task<ServiceResult<IReadOnlyList<AdminPackageDto>>> ListPackagesAsync(CancellationToken ct = default);
    Task<ServiceResult<AdminPackageDto>> CreatePackageAsync(long actorId, SavePackageDto dto, CancellationToken ct = default);
    Task<ServiceResult<AdminPackageDto>> UpdatePackageAsync(long actorId, long id, SavePackageDto dto, CancellationToken ct = default);
    Task<ServiceResult<AdminPackageDto>> PublishPackageAsync(long actorId, long id, CancellationToken ct = default);
    Task<ServiceResult<AdminPackageDto>> ArchivePackageAsync(long actorId, long id, CancellationToken ct = default);
    Task<ServiceResult<AdminPackageDto>> NewPackageVersionAsync(long actorId, long id, string version, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<AdminPlanDto>>> ListPlansAsync(CancellationToken ct = default);
    Task<ServiceResult<AdminPlanDto>> CreatePlanAsync(long actorId, SavePlanDto dto, CancellationToken ct = default);
    Task<ServiceResult<AdminPlanDto>> UpdatePlanAsync(long actorId, long id, SavePlanDto dto, CancellationToken ct = default);
    Task<ServiceResult<AdminPlanDto>> PublishPlanAsync(long actorId, long id, CancellationToken ct = default);
    Task<ServiceResult<AdminPlanDto>> ArchivePlanAsync(long actorId, long id, CancellationToken ct = default);
}

/// <summary>
/// Editing the catalogue.
///
/// One rule shapes every method here: **a published version is never edited.** Editing is only
/// possible while a row is a draft; changing something already on sale means creating the next
/// version and publishing that. Orders bound to the old version keep their terms, which is the
/// entire reason the catalogue is versioned (BR-017).
///
/// Every mutation is audited. Prices and package composition are commercial terms, and "who
/// changed the price of the basic package, and when" is a question that gets asked during a
/// dispute, not during development.
/// </summary>
public sealed class CatalogAdminService : ICatalogAdminService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CatalogAdminService> _logger;

    public CatalogAdminService(AppDbContext db, ILogger<CatalogAdminService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ---------------------------------------------------------------------------------------------
    // Packages
    // ---------------------------------------------------------------------------------------------

    public async Task<ServiceResult<IReadOnlyList<AdminPackageDto>>> ListPackagesAsync(CancellationToken ct = default)
    {
        var packages = await _db.ServicePackages
            .AsNoTracking()
            .Include(p => p.ChecklistTemplate)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ThenByDescending(p => p.Version)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<AdminPackageDto>>.Ok(packages.Select(MapPackage).ToList());
    }

    public async Task<ServiceResult<AdminPackageDto>> CreatePackageAsync(
        long actorId, SavePackageDto dto, CancellationToken ct = default)
    {
        var validation = ValidatePackage(dto);
        if (validation is not null)
        {
            return validation;
        }

        var code = dto.Code.Trim().ToLowerInvariant();
        var version = dto.Version.Trim();

        if (await _db.ServicePackages.AnyAsync(p => p.Code == code && p.Version == version && p.Locale == "ru", ct))
        {
            return ServiceResult<AdminPackageDto>.Validation(
                $"Пакет «{code}» версии {version} уже существует.", new { field = "version" });
        }

        var package = new ServicePackage
        {
            Code = code,
            Version = version,
            Locale = "ru",
            Status = ServicePackageStatuses.Draft,
        };

        ApplyPackage(package, dto);
        package.ChecklistTemplate = BuildChecklist(dto);

        _db.ServicePackages.Add(package);
        await _db.SaveChangesAsync(ct);

        Audit(actorId, "package_created", package.Code, package.Version);
        return ServiceResult<AdminPackageDto>.Created(MapPackage(package));
    }

    public async Task<ServiceResult<AdminPackageDto>> UpdatePackageAsync(
        long actorId, long id, SavePackageDto dto, CancellationToken ct = default)
    {
        var package = await _db.ServicePackages
            .Include(p => p.ChecklistTemplate)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (package is null)
        {
            return ServiceResult<AdminPackageDto>.NotFound("Пакет не найден.");
        }

        if (package.Status != ServicePackageStatuses.Draft)
        {
            // Refused rather than quietly branched: silently creating a version behind the
            // editor's back is how two people end up believing different things are on sale.
            return ServiceResult<AdminPackageDto>.Validation(
                "Опубликованную версию нельзя изменить. Создайте новую версию.");
        }

        var validation = ValidatePackage(dto);
        if (validation is not null)
        {
            return validation;
        }

        ApplyPackage(package, dto);

        var checklist = BuildChecklist(dto);
        if (package.ChecklistTemplate is null)
        {
            package.ChecklistTemplate = checklist;
        }
        else
        {
            package.ChecklistTemplate.Items = checklist.Items;
            package.ChecklistTemplate.RequiredMedia = checklist.RequiredMedia;
        }

        await _db.SaveChangesAsync(ct);
        Audit(actorId, "package_updated", package.Code, package.Version);

        return ServiceResult<AdminPackageDto>.Ok(MapPackage(package));
    }

    public async Task<ServiceResult<AdminPackageDto>> PublishPackageAsync(
        long actorId, long id, CancellationToken ct = default)
    {
        var package = await _db.ServicePackages
            .Include(p => p.ChecklistTemplate)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (package is null)
        {
            return ServiceResult<AdminPackageDto>.NotFound("Пакет не найден.");
        }

        if (package.Status == ServicePackageStatuses.Published)
        {
            return ServiceResult<AdminPackageDto>.Ok(MapPackage(package));
        }

        if (package.Status == ServicePackageStatuses.Archived)
        {
            return ServiceResult<AdminPackageDto>.Validation("Архивную версию нельзя опубликовать заново.");
        }

        // A package with no checklist has no definition of "done", so QA would have nothing to
        // measure a visit against. Better to refuse than to sell it.
        var hasItems = package.ChecklistTemplate?.Items?.RootElement.GetArrayLength() > 0;
        if (hasItems != true)
        {
            return ServiceResult<AdminPackageDto>.Validation(
                "Нельзя опубликовать пакет без чек-листа: по нему проверяется выполненная работа.");
        }

        package.Status = ServicePackageStatuses.Published;
        package.PublishedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        Audit(actorId, "package_published", package.Code, package.Version);

        return ServiceResult<AdminPackageDto>.Ok(MapPackage(package));
    }

    public async Task<ServiceResult<AdminPackageDto>> ArchivePackageAsync(
        long actorId, long id, CancellationToken ct = default)
    {
        var package = await _db.ServicePackages
            .Include(p => p.ChecklistTemplate)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (package is null)
        {
            return ServiceResult<AdminPackageDto>.NotFound("Пакет не найден.");
        }

        // Archiving takes it off sale; orders already bound to this version are untouched.
        package.Status = ServicePackageStatuses.Archived;
        await _db.SaveChangesAsync(ct);
        Audit(actorId, "package_archived", package.Code, package.Version);

        return ServiceResult<AdminPackageDto>.Ok(MapPackage(package));
    }

    public async Task<ServiceResult<AdminPackageDto>> NewPackageVersionAsync(
        long actorId, long id, string version, CancellationToken ct = default)
    {
        var source = await _db.ServicePackages
            .AsNoTracking()
            .Include(p => p.ChecklistTemplate)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (source is null)
        {
            return ServiceResult<AdminPackageDto>.NotFound("Пакет не найден.");
        }

        var next = version.Trim();
        if (string.IsNullOrWhiteSpace(next))
        {
            return ServiceResult<AdminPackageDto>.Validation("Укажите номер новой версии.", new { field = "version" });
        }

        if (await _db.ServicePackages.AnyAsync(p => p.Code == source.Code && p.Version == next && p.Locale == "ru", ct))
        {
            return ServiceResult<AdminPackageDto>.Validation(
                $"Версия {next} уже существует.", new { field = "version" });
        }

        // Copied as a draft, so the editor changes what they meant to change and publishes when
        // ready — the live version stays on sale untouched in the meantime.
        var copy = new ServicePackage
        {
            Code = source.Code,
            Version = next,
            Locale = source.Locale,
            Title = source.Title,
            Summary = source.Summary,
            Includes = Clone(source.Includes),
            Limits = Clone(source.Limits),
            PriceFromRub = source.PriceFromRub,
            WarrantyDays = source.WarrantyDays,
            VisitsLabel = source.VisitsLabel,
            SortOrder = source.SortOrder,
            Status = ServicePackageStatuses.Draft,
            ChecklistTemplate = new ChecklistTemplate
            {
                Items = Clone(source.ChecklistTemplate?.Items),
                RequiredMedia = Clone(source.ChecklistTemplate?.RequiredMedia),
            },
        };

        _db.ServicePackages.Add(copy);
        await _db.SaveChangesAsync(ct);
        Audit(actorId, "package_version_created", copy.Code, copy.Version);

        return ServiceResult<AdminPackageDto>.Created(MapPackage(copy));
    }

    // ---------------------------------------------------------------------------------------------
    // Subscription plans
    // ---------------------------------------------------------------------------------------------

    public async Task<ServiceResult<IReadOnlyList<AdminPlanDto>>> ListPlansAsync(CancellationToken ct = default)
    {
        var plans = await _db.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ThenByDescending(p => p.Version)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<AdminPlanDto>>.Ok(plans.Select(MapPlan).ToList());
    }

    public async Task<ServiceResult<AdminPlanDto>> CreatePlanAsync(
        long actorId, SavePlanDto dto, CancellationToken ct = default)
    {
        var validation = ValidatePlan(dto);
        if (validation is not null)
        {
            return validation;
        }

        var code = dto.Code.Trim().ToLowerInvariant();
        var version = dto.Version.Trim();

        if (await _db.SubscriptionPlans.AnyAsync(p => p.Code == code && p.Version == version && p.Locale == "ru", ct))
        {
            return ServiceResult<AdminPlanDto>.Validation(
                $"План «{code}» версии {version} уже существует.", new { field = "version" });
        }

        var plan = new SubscriptionPlan
        {
            Code = code,
            Version = version,
            Locale = "ru",
            Status = ServicePackageStatuses.Draft,
        };

        ApplyPlan(plan, dto);
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        Audit(actorId, "plan_created", plan.Code, plan.Version);

        return ServiceResult<AdminPlanDto>.Created(MapPlan(plan));
    }

    public async Task<ServiceResult<AdminPlanDto>> UpdatePlanAsync(
        long actorId, long id, SavePlanDto dto, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return ServiceResult<AdminPlanDto>.NotFound("План не найден.");
        }

        if (plan.Status != ServicePackageStatuses.Draft)
        {
            return ServiceResult<AdminPlanDto>.Validation(
                "Опубликованный план нельзя изменить. Создайте новую версию.");
        }

        var validation = ValidatePlan(dto);
        if (validation is not null)
        {
            return validation;
        }

        ApplyPlan(plan, dto);
        await _db.SaveChangesAsync(ct);
        Audit(actorId, "plan_updated", plan.Code, plan.Version);

        return ServiceResult<AdminPlanDto>.Ok(MapPlan(plan));
    }

    public async Task<ServiceResult<AdminPlanDto>> PublishPlanAsync(
        long actorId, long id, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return ServiceResult<AdminPlanDto>.NotFound("План не найден.");
        }

        if (plan.Status == ServicePackageStatuses.Archived)
        {
            return ServiceResult<AdminPlanDto>.Validation("Архивный план нельзя опубликовать заново.");
        }

        // A plan pointing at a package nobody sells would create visits with no checklist.
        var packageExists = await _db.ServicePackages.AnyAsync(
            p => p.Code == plan.ServicePackageCode && p.Status == ServicePackageStatuses.Published, ct);

        if (!packageExists)
        {
            return ServiceResult<AdminPlanDto>.Validation(
                $"Пакет «{plan.ServicePackageCode}» не опубликован — план не на что опереть.");
        }

        plan.Status = ServicePackageStatuses.Published;
        plan.PublishedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        Audit(actorId, "plan_published", plan.Code, plan.Version);

        return ServiceResult<AdminPlanDto>.Ok(MapPlan(plan));
    }

    public async Task<ServiceResult<AdminPlanDto>> ArchivePlanAsync(
        long actorId, long id, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return ServiceResult<AdminPlanDto>.NotFound("План не найден.");
        }

        plan.Status = ServicePackageStatuses.Archived;
        await _db.SaveChangesAsync(ct);
        Audit(actorId, "plan_archived", plan.Code, plan.Version);

        return ServiceResult<AdminPlanDto>.Ok(MapPlan(plan));
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private static ServiceResult<AdminPackageDto>? ValidatePackage(SavePackageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return ServiceResult<AdminPackageDto>.Validation("Укажите код пакета.", new { field = "code" });
        if (string.IsNullOrWhiteSpace(dto.Version))
            return ServiceResult<AdminPackageDto>.Validation("Укажите версию.", new { field = "version" });
        if (string.IsNullOrWhiteSpace(dto.Title))
            return ServiceResult<AdminPackageDto>.Validation("Укажите название.", new { field = "title" });
        if (dto.PriceFromRub <= 0)
            return ServiceResult<AdminPackageDto>.Validation("Цена должна быть больше нуля.", new { field = "priceFromRub" });
        if (dto.WarrantyDays < 0)
            return ServiceResult<AdminPackageDto>.Validation("Гарантия не может быть отрицательной.", new { field = "warrantyDays" });
        return null;
    }

    private static ServiceResult<AdminPlanDto>? ValidatePlan(SavePlanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return ServiceResult<AdminPlanDto>.Validation("Укажите код плана.", new { field = "code" });
        if (string.IsNullOrWhiteSpace(dto.Version))
            return ServiceResult<AdminPlanDto>.Validation("Укажите версию.", new { field = "version" });
        if (string.IsNullOrWhiteSpace(dto.Title))
            return ServiceResult<AdminPlanDto>.Validation("Укажите название.", new { field = "title" });
        if (dto.VisitsTotal <= 0)
            return ServiceResult<AdminPlanDto>.Validation("Количество визитов должно быть больше нуля.", new { field = "visitsTotal" });
        if (dto.PeriodMonths <= 0)
            return ServiceResult<AdminPlanDto>.Validation("Период должен быть больше нуля.", new { field = "periodMonths" });
        if (dto.PriceRub <= 0)
            return ServiceResult<AdminPlanDto>.Validation("Цена должна быть больше нуля.", new { field = "priceRub" });
        return null;
    }

    private static void ApplyPackage(ServicePackage package, SavePackageDto dto)
    {
        package.Title = dto.Title.Trim();
        package.Summary = dto.Summary?.Trim() ?? string.Empty;
        package.PriceFromRub = dto.PriceFromRub;
        package.WarrantyDays = dto.WarrantyDays;
        package.VisitsLabel = string.IsNullOrWhiteSpace(dto.VisitsLabel) ? null : dto.VisitsLabel.Trim();
        package.SortOrder = dto.SortOrder;
        package.Includes = Lines(dto.Includes);
        package.Limits = Lines(dto.Limits);
    }

    private static void ApplyPlan(SubscriptionPlan plan, SavePlanDto dto)
    {
        plan.Title = dto.Title.Trim();
        plan.Summary = dto.Summary?.Trim() ?? string.Empty;
        plan.ServicePackageCode = dto.ServicePackageCode.Trim().ToLowerInvariant();
        plan.VisitsTotal = dto.VisitsTotal;
        plan.PeriodMonths = dto.PeriodMonths;
        plan.PriceRub = dto.PriceRub;
        plan.SortOrder = dto.SortOrder;
    }

    private static ChecklistTemplate BuildChecklist(SavePackageDto dto) => new()
    {
        Items = JsonDocument.Parse(JsonSerializer.Serialize(
            (dto.ChecklistItems ?? new List<SaveChecklistItemDto>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Title))
                .Select(i => new
                {
                    key = string.IsNullOrWhiteSpace(i.Key) ? Slug(i.Title) : i.Key.Trim(),
                    title = i.Title.Trim(),
                    optional = i.Optional,
                }))),
        RequiredMedia = JsonDocument.Parse(JsonSerializer.Serialize(
            (dto.RequiredMedia ?? new List<SaveRequiredMediaDto>())
                .Where(m => !string.IsNullOrWhiteSpace(m.Phase) && m.MinCount > 0)
                .Select(m => new
                {
                    phase = m.Phase.Trim(),
                    minCount = m.MinCount,
                    description = m.Description?.Trim() ?? string.Empty,
                }))),
    };

    private static string Slug(string title)
    {
        var chars = title.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        return new string(chars).Trim('_');
    }

    private static JsonDocument? Lines(IReadOnlyList<string>? values) =>
        values is null
            ? JsonDocument.Parse("[]")
            : JsonDocument.Parse(JsonSerializer.Serialize(
                values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim())));

    private static JsonDocument? Clone(JsonDocument? source) =>
        source is null ? null : JsonDocument.Parse(source.RootElement.GetRawText());

    private static AdminPackageDto MapPackage(ServicePackage p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Version = p.Version,
        Title = p.Title,
        Summary = p.Summary,
        Includes = ReadLines(p.Includes),
        Limits = ReadLines(p.Limits),
        PriceFromRub = p.PriceFromRub,
        WarrantyDays = p.WarrantyDays,
        VisitsLabel = p.VisitsLabel,
        SortOrder = p.SortOrder,
        Status = p.Status,
        PublishedAt = p.PublishedAt,
        Editable = p.Status == ServicePackageStatuses.Draft,
        ChecklistItems = ReadChecklist(p.ChecklistTemplate?.Items),
        RequiredMedia = ReadMedia(p.ChecklistTemplate?.RequiredMedia),
    };

    private static AdminPlanDto MapPlan(SubscriptionPlan p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Version = p.Version,
        Title = p.Title,
        Summary = p.Summary,
        ServicePackageCode = p.ServicePackageCode,
        VisitsTotal = p.VisitsTotal,
        PeriodMonths = p.PeriodMonths,
        PriceRub = p.PriceRub,
        PricePerVisit = p.PricePerVisit,
        SortOrder = p.SortOrder,
        Status = p.Status,
        PublishedAt = p.PublishedAt,
        Editable = p.Status == ServicePackageStatuses.Draft,
    };

    private static IReadOnlyList<string> ReadLines(JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return doc.RootElement.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
    }

    private static IReadOnlyList<SaveChecklistItemDto> ReadChecklist(JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<SaveChecklistItemDto>();

        return doc.RootElement.EnumerateArray().Select(e => new SaveChecklistItemDto
        {
            Key = e.TryGetProperty("key", out var k) ? k.GetString() : null,
            Title = e.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
            Optional = e.TryGetProperty("optional", out var o) && o.ValueKind == JsonValueKind.True,
        }).ToList();
    }

    private static IReadOnlyList<SaveRequiredMediaDto> ReadMedia(JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<SaveRequiredMediaDto>();

        return doc.RootElement.EnumerateArray().Select(e => new SaveRequiredMediaDto
        {
            Phase = e.TryGetProperty("phase", out var p) ? p.GetString() ?? string.Empty : string.Empty,
            MinCount = e.TryGetProperty("minCount", out var m) && m.TryGetInt32(out var v) ? v : 0,
            Description = e.TryGetProperty("description", out var d) ? d.GetString() : null,
        }).ToList();
    }

    private void Audit(long actorId, string action, string code, string version)
    {
        // Identifiers and the action only. Prices are commercial terms, and the row that matters
        // is the versioned catalogue entry itself, not a copy of its numbers in a log.
        _logger.LogInformation(
            "Catalog {Action}: {Code} v{Version} by user {ActorId}", action, code, version, actorId);

        _db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            UserId = actorId,
            EventType = SecurityAuditEventTypes.CatalogChanged,
            ActorRole = UserRoles.Admin,
            Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { action, code, version })),
        });
    }
}
