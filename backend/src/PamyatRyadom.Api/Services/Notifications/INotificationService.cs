namespace PamyatRyadom.Api.Services.Notifications;

public interface INotificationService
{
    /// <summary>Sends the notification registered for <paramref name="eventType"/> to
    /// <paramref name="recipientEmail"/>. Returns <c>false</c> instead of throwing when delivery
    /// itself fails, so a business operation (an order transition, a payment, a dispute decision)
    /// never rolls back because a mail server was unreachable — the failure is logged by event
    /// type and a pseudonymous recipient key, never by address.
    ///
    /// Throws when the caller is wrong rather than the mail server: an unregistered
    /// <paramref name="eventType"/>, or a <paramref name="recipientRole"/> that does not match what
    /// the template is written for.</summary>
    Task<bool> SendAsync(
        string eventType,
        string recipientEmail,
        string recipientRole,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default);
}
