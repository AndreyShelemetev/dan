namespace PamyatRyadom.Api.Services.Notifications;

public interface INotificationTemplateCatalog
{
    bool TryGet(string eventType, out NotificationTemplate template);
}

/// <summary>
/// The templates for every notification event the system can send. Empty today: this task builds
/// the delivery machinery only, the events themselves (estimate published, payment received, visit
/// assigned, dispute resolved, ...) are added here by the tasks that wire each one up, the same way
/// a template is added — never edited in place once a business event depends on its wording, since
/// that would silently change what an in-flight notification says.
/// </summary>
public sealed class NotificationTemplateCatalog : INotificationTemplateCatalog
{
    private static readonly IReadOnlyDictionary<string, NotificationTemplate> Templates =
        new Dictionary<string, NotificationTemplate>();

    public bool TryGet(string eventType, out NotificationTemplate template) =>
        Templates.TryGetValue(eventType, out template!);
}
