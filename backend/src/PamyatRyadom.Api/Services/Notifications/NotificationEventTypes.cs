namespace PamyatRyadom.Api.Services.Notifications;

/// <summary>
/// Every event type a template is registered under in <see cref="NotificationTemplateCatalog"/>.
/// Shared between the catalog and the services that trigger a send, so a typo in either place is a
/// compiler error rather than a silently unsent email.
/// </summary>
public static class NotificationEventTypes
{
    public const string EstimatePublished = "estimate_published";
    public const string PaymentReceived = "payment_received";
    public const string ReportReady = "report_ready";
    public const string VisitAssigned = "visit_assigned";
}
