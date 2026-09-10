using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Models.Catalog;

namespace PamyatRyadom.Api.Services.Catalog;

/// <summary>
/// The initial published catalogue.
///
/// Prices come from the business concept and are explicitly a **pilot hypothesis**, not settled
/// pricing: the concept itself says they have to be re-derived from real minutes, kilometres and
/// materials after the first paid orders, and the geography has since widened from one city to
/// the country, which changes route density — the single biggest lever on the numbers.
///
/// Changing a price is therefore expected. What must never happen is changing it *in place*:
/// publish a new version, and orders already sold keep the terms they were sold under. This
/// seeder only ever inserts versions that are missing, so re-running it cannot rewrite history.
/// </summary>
public interface ICatalogSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

public sealed class CatalogSeeder : ICatalogSeeder
{
    private const string Version = "1.0";

    private readonly AppDbContext _db;
    private readonly ILogger<CatalogSeeder> _logger;

    public CatalogSeeder(AppDbContext db, ILogger<CatalogSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var definition in Definitions)
        {
            var exists = await _db.ServicePackages.AnyAsync(
                p => p.Code == definition.Package.Code
                     && p.Version == definition.Package.Version
                     && p.Locale == definition.Package.Locale,
                ct);

            if (exists)
            {
                continue;
            }

            _db.ServicePackages.Add(definition.Package);
            definition.Package.ChecklistTemplate = definition.Checklist;
            _logger.LogInformation(
                "Seeded service package {Code} v{Version}", definition.Package.Code, definition.Package.Version);
        }

        if (_db.ChangeTracker.HasChanges())
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Another instance seeded first. Losing this race is the expected outcome, not a
                // failure worth taking the API down for.
                _db.ChangeTracker.Clear();
            }
        }
    }

    private static JsonDocument Json(params string[] lines) =>
        JsonDocument.Parse(JsonSerializer.Serialize(lines));

    private static JsonDocument Checklist(params ChecklistItem[] items) =>
        JsonDocument.Parse(JsonSerializer.Serialize(items, JsonOptions));

    private static JsonDocument Media(params RequiredMediaRule[] rules) =>
        JsonDocument.Parse(JsonSerializer.Serialize(rules, JsonOptions));

    /// <summary>Shared with every reader — see <see cref="ChecklistJson"/>.</summary>
    private static readonly JsonSerializerOptions JsonOptions = ChecklistJson.Options;

    private static readonly DateTimeOffset PublishedAt = new(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);

    private sealed record Definition(ServicePackage Package, ChecklistTemplate Checklist);

    private static ServicePackage Package(
        string code, string title, string summary, decimal price, int warrantyDays,
        string visits, int sort, JsonDocument includes, JsonDocument limits) =>
        new()
        {
            Code = code,
            Version = Version,
            Locale = "ru",
            Title = title,
            Summary = summary,
            Includes = includes,
            Limits = limits,
            PriceFromRub = price,
            WarrantyDays = warrantyDays,
            VisitsLabel = visits,
            SortOrder = sort,
            Status = ServicePackageStatuses.Published,
            PublishedAt = PublishedAt,
        };

    private static readonly IReadOnlyList<Definition> Definitions = new List<Definition>
    {
        // Sold first when the description is too thin to dispatch against — the LOCATION_REVIEW
        // branch. Deliberately cheap and deliberately not a cleaning package.
        new(
            Package(
                ServicePackageCodes.Inspection,
                "Осмотр и цифровой паспорт",
                "Исполнитель находит место, фотографирует его состояние и составляет описание. Стоимость засчитывается в заказ ухода, если вы закажете его в течение 30 дней.",
                1_900m, 14, "1 визит", 1,
                Json(
                    "Поиск места по вашим данным",
                    "8–12 фотографий состояния",
                    "Координата и схема прохода",
                    "Описание состояния и предварительная смета"),
                Json(
                    "Не включает уборку и работы по благоустройству",
                    "Если место не найдено, мы вернём стоимость")),
            new ChecklistTemplate
            {
                Items = Checklist(
                    new ChecklistItem("verify_location", "Подтвердить место по двум признакам"),
                    new ChecklistItem("photo_state", "Снять состояние участка"),
                    new ChecklistItem("describe_access", "Описать проход и ориентиры"),
                    new ChecklistItem("note_damage", "Зафиксировать повреждения", Optional: true)),
                RequiredMedia = Media(
                    new RequiredMediaRule("before", 8, "Общий вид, памятник, ограда, надпись, проблемные зоны")),
            }),

        new(
            Package(
                ServicePackageCodes.Basic,
                "Базовый уход",
                "Регулярная уборка участка: мусор и листва, прополка, бережная мойка памятника, протирка ограды и фотоотчёт.",
                4_900m, 14, "1 визит", 2,
                Json(
                    "Уборка мусора и листвы",
                    "Ручная прополка",
                    "Бережная мойка стандартного памятника",
                    "Протирка ограды",
                    "Фотоотчёт «до и после»"),
                Json(
                    "До 5 м² участка",
                    "Без стойких загрязнений, покраски и вывоза крупного мусора",
                    "Без применения агрессивной химии к памятнику")),
            new ChecklistTemplate
            {
                Items = Checklist(
                    new ChecklistItem("verify_location", "Подтвердить место по двум признакам"),
                    new ChecklistItem("photo_before", "Снять обязательные ракурсы «до»"),
                    new ChecklistItem("collect_litter", "Убрать мусор и листву"),
                    new ChecklistItem("weeding", "Прополоть участок"),
                    new ChecklistItem("wash_monument", "Бережно вымыть памятник"),
                    new ChecklistItem("wipe_fence", "Протереть ограду"),
                    new ChecklistItem("remove_waste", "Вынести мусор в разрешённое место"),
                    new ChecklistItem("photo_after", "Повторить ракурсы «после»")),
                RequiredMedia = Media(
                    new RequiredMediaRule("before", 4, "Общий вид, памятник, ограда, проблемные зоны"),
                    new RequiredMediaRule("after", 4, "Те же ракурсы после работ")),
            }),

        new(
            Package(
                ServicePackageCodes.Seasonal,
                "Сезонный уход",
                "Подготовка места к сезону: всё из базового ухода, плюс работа с налётом, обрезка простого кустарника и подсыпка грунта.",
                8_400m, 30, "1–2 визита", 3,
                Json(
                    "Всё из базового ухода",
                    "Снятие мха и налёта безопасным средством",
                    "Обрезка простого кустарника",
                    "Подсыпка грунта или песка в пределах лимита",
                    "Рекомендации по состоянию памятника"),
                Json(
                    "Назначается после осмотра",
                    "Материалы в пределах согласованного лимита",
                    "Реставрация и ремонт конструкций не входят")),
            new ChecklistTemplate
            {
                Items = Checklist(
                    new ChecklistItem("verify_location", "Подтвердить место по двум признакам"),
                    new ChecklistItem("photo_before", "Снять обязательные ракурсы «до»"),
                    new ChecklistItem("collect_litter", "Убрать мусор и листву"),
                    new ChecklistItem("weeding", "Прополоть участок"),
                    new ChecklistItem("wash_monument", "Бережно вымыть памятник"),
                    new ChecklistItem("remove_moss", "Снять мох и налёт"),
                    new ChecklistItem("trim_shrubs", "Обрезать кустарник", Optional: true),
                    new ChecklistItem("add_soil", "Подсыпать грунт или песок", Optional: true),
                    new ChecklistItem("remove_waste", "Вынести мусор в разрешённое место"),
                    new ChecklistItem("photo_after", "Повторить ракурсы «после»")),
                RequiredMedia = Media(
                    new RequiredMediaRule("before", 4, "Общий вид, памятник, ограда, проблемные зоны"),
                    new RequiredMediaRule("process", 1, "Использованные материалы"),
                    new RequiredMediaRule("after", 4, "Те же ракурсы после работ")),
            }),

        new(
            Package(
                ServicePackageCodes.Flowers,
                "Цветы к памятной дате",
                "Покупка и возложение цветов к нужной дате с фотографией результата. Можно совместить с уборкой.",
                1_200m, 7, "1 визит", 4,
                Json(
                    "Покупка цветов по вашему выбору",
                    "Доставка и возложение",
                    "Фотография результата в тот же день"),
                Json(
                    "Стоимость букета оплачивается отдельно",
                    "Зависит от доступности слота и товара",
                    "Без обещания точного времени до минуты")),
            new ChecklistTemplate
            {
                Items = Checklist(
                    new ChecklistItem("verify_location", "Подтвердить место по двум признакам"),
                    new ChecklistItem("place_flowers", "Возложить цветы"),
                    new ChecklistItem("photo_after", "Снять результат")),
                // No "before" set: nothing is being changed about the plot, and demanding four
                // angles of an untouched grave is ceremony rather than evidence.
                RequiredMedia = Media(
                    new RequiredMediaRule("after", 2, "Общий вид и крупный план цветов")),
            }),
    };
}
