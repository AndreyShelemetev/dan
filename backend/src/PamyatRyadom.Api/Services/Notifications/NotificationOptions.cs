namespace PamyatRyadom.Api.Services.Notifications;

/// <summary>
/// Bound from the <c>Notifications</c> configuration section.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>The site's own public origin, used to build the link a notification email points
    /// at (e.g. an order page). Same default as <c>PaymentOptions.ReturnUrlBase</c> — the address
    /// a browser can actually reach, not the address the API talks to itself.</summary>
    public string SiteBaseUrl { get; set; } = "http://localhost:3100";
}
