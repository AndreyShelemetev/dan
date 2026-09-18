namespace PamyatRyadom.Api.Services.Notifications;

public interface INotificationTemplateCatalog
{
    bool TryGet(string eventType, out NotificationTemplate template);
}

/// <summary>
/// The templates for every notification event the system can send. A template is added here by the
/// task that wires up its event — never edited in place once a business event depends on its
/// wording, since that would silently change what an in-flight notification says; a wording change
/// needs a new event type, the same discipline <c>LegalDocumentRegistry</c> applies to a document
/// version.
/// </summary>
public sealed class NotificationTemplateCatalog : INotificationTemplateCatalog
{
    private static readonly IReadOnlyDictionary<string, NotificationTemplate> Templates =
        new Dictionary<string, NotificationTemplate>
        {
            [NotificationEventTypes.EstimatePublished] = new NotificationTemplate
            {
                EventType = NotificationEventTypes.EstimatePublished,
                RecipientRole = NotificationRecipientRoles.Client,
                BuildSubject = p => $"Смета по заказу {p["orderNumber"]} готова",
                BuildParagraphs = p => new[]
                {
                    $"По заказу {p["orderNumber"]} подготовлена смета.",
                    "Посмотрите её и подтвердите, если условия устраивают.",
                },
                LinkPathParameter = "orderPath",
                LinkLabel = "Посмотреть смету",
            },

            [NotificationEventTypes.PaymentReceived] = new NotificationTemplate
            {
                EventType = NotificationEventTypes.PaymentReceived,
                RecipientRole = NotificationRecipientRoles.Client,
                BuildSubject = p => $"Оплата по заказу {p["orderNumber"]} получена",
                BuildParagraphs = p => new[]
                {
                    $"Оплата по заказу {p["orderNumber"]} получена.",
                    "Мы передаём заказ в работу и сообщим, когда исполнитель приступит.",
                },
                LinkPathParameter = "orderPath",
                LinkLabel = "Открыть заказ",
            },

            [NotificationEventTypes.ReportReady] = new NotificationTemplate
            {
                EventType = NotificationEventTypes.ReportReady,
                RecipientRole = NotificationRecipientRoles.Client,
                BuildSubject = p => $"Отчёт по заказу {p["orderNumber"]} готов",
                BuildParagraphs = p => new[]
                {
                    $"Исполнитель выполнил работу по заказу {p["orderNumber"]}.",
                    "Отчёт прошёл проверку и доступен для просмотра.",
                },
                LinkPathParameter = "orderPath",
                LinkLabel = "Посмотреть отчёт",
            },

            [NotificationEventTypes.VisitAssigned] = new NotificationTemplate
            {
                EventType = NotificationEventTypes.VisitAssigned,
                RecipientRole = NotificationRecipientRoles.Executor,
                BuildSubject = p => $"Новый визит — заказ {p["orderNumber"]}",
                BuildParagraphs = p => new[]
                {
                    $"Вам назначен визит по заказу {p["orderNumber"]}.",
                    "Адрес и чек-лист смотрите по ссылке — здесь их нет намеренно.",
                },
                LinkPathParameter = "visitPath",
                LinkLabel = "Открыть визит",
            },
        };

    public bool TryGet(string eventType, out NotificationTemplate template) =>
        Templates.TryGetValue(eventType, out template!);
}
