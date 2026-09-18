namespace PamyatRyadom.Api.Services.Notifications;

/// <summary>
/// Who a notification template is written for. Every template declares exactly one of these, and
/// <see cref="NotificationService"/> refuses to send it to any other audience — the same kind of
/// guard that keeps <c>VisitReportDto</c> from ever carrying an executor's payout: a client-facing
/// template can never be misrouted to an executor's inbox, or the other way round.
/// </summary>
public static class NotificationRecipientRoles
{
    public const string Client = "client";
    public const string Executor = "executor";
    public const string Staff = "staff";
}
