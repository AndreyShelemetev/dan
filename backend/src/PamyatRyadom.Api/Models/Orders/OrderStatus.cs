namespace PamyatRyadom.Api.Models.Orders;

/// <summary>
/// The order lifecycle.
///
/// Payment, visit, QA and dispute keep their own state; this is the aggregate that an operator
/// and a client both read. Deliberately separate machines, because folding payment state into
/// the order means every retried webhook has to reason about the order — see the module plan.
/// </summary>
public static class OrderStatuses
{
    /// <summary>Being filled in. Visible only to its author.</summary>
    public const string Draft = "draft";

    /// <summary>Submitted; operations have it in the queue.</summary>
    public const string Submitted = "submitted";

    /// <summary>The place cannot be found from what was supplied. The service sells an
    /// inspection rather than dispatching someone to search blind.</summary>
    public const string LocationReview = "location_review";

    /// <summary>An estimate version is published and waiting on the client.</summary>
    public const string EstimateReady = "estimate_ready";

    /// <summary>The client accepted a specific estimate version. Only now may money be taken.</summary>
    public const string AwaitingPayment = "awaiting_payment";

    /// <summary>Payment confirmed by the provider — never by a browser redirect.</summary>
    public const string Paid = "paid";

    public const string Assigning = "assigning";
    public const string Assigned = "assigned";
    public const string InProgress = "in_progress";

    /// <summary>Extra work found on site, waiting on the client's decision. Nothing is done and
    /// nothing is charged until they answer.</summary>
    public const string ExtraApproval = "extra_approval";

    public const string QaReview = "qa_review";

    /// <summary>QA approved the report and the client can now see it.</summary>
    public const string CustomerReview = "customer_review";

    public const string Completed = "completed";
    public const string Disputed = "disputed";
    public const string Cancelled = "cancelled";
    public const string Refunded = "refunded";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Draft, Submitted, LocationReview, EstimateReady, AwaitingPayment, Paid, Assigning,
        Assigned, InProgress, ExtraApproval, QaReview, CustomerReview, Completed, Disputed,
        Cancelled, Refunded,
    };

    /// <summary>Terminal states: nothing moves out of them.</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Completed, Cancelled, Refunded };
}

/// <summary>
/// What the client is told, and what they can do about it.
///
/// Internal statuses are an operational vocabulary — "assigning" and "qa_review" describe our
/// process, not their situation. The public text answers the only question they actually have:
/// what is happening, and what is expected of me.
/// </summary>
public sealed record PublicOrderState(string Label, string? Cta);

public static class OrderStatusPresentation
{
    private static readonly Dictionary<string, PublicOrderState> Map = new()
    {
        [OrderStatuses.Draft] = new("Черновик", "Продолжить"),
        [OrderStatuses.Submitted] = new("Проверяем данные", null),
        [OrderStatuses.LocationReview] = new("Уточняем место", "Добавить сведения"),
        [OrderStatuses.EstimateReady] = new("Смета готова", "Посмотреть и подтвердить"),
        [OrderStatuses.AwaitingPayment] = new("Ожидается оплата", "Оплатить"),
        [OrderStatuses.Paid] = new("Организуем выполнение", null),
        [OrderStatuses.Assigning] = new("Организуем выполнение", null),
        [OrderStatuses.Assigned] = new("Исполнитель назначен", null),
        [OrderStatuses.InProgress] = new("Работа выполняется", null),
        [OrderStatuses.ExtraApproval] = new("Нужно ваше решение", "Согласовать допработу"),
        [OrderStatuses.QaReview] = new("Проверяем качество", null),
        [OrderStatuses.CustomerReview] = new("Отчёт готов", "Принять или сообщить о проблеме"),
        [OrderStatuses.Completed] = new("Завершено", "Повторить заказ"),
        [OrderStatuses.Disputed] = new("Обращение рассматривается", "Открыть переписку"),
        [OrderStatuses.Cancelled] = new("Отменено", null),
        [OrderStatuses.Refunded] = new("Средства возвращены", null),
    };

    public static PublicOrderState For(string status) =>
        Map.TryGetValue(status, out var state) ? state : new PublicOrderState("В работе", null);
}
