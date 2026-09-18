namespace PamyatRyadom.Api.Services.Notifications;

/// <summary>
/// The Russian text behind one notification event: a subject line, a body written as plain
/// paragraphs (rendered into both the HTML and the plain-text part of the email — see
/// <see cref="NotificationBodyRenderer"/>), and, optionally, a link to the one page the recipient
/// needs next. Declared once per event type in <see cref="INotificationTemplateCatalog"/>, the same
/// way <c>LegalDocumentRegistry</c> is the one place a document's version is declared.
/// </summary>
public sealed class NotificationTemplate
{
    public required string EventType { get; init; }

    /// <summary>One of <see cref="NotificationRecipientRoles"/>. <see cref="NotificationService"/>
    /// refuses to send this template to any other role.</summary>
    public required string RecipientRole { get; init; }

    public required Func<IReadOnlyDictionary<string, string>, string> BuildSubject { get; init; }

    /// <summary>Each entry is one paragraph, in order. Kept as plain sentences — no markup — so the
    /// same text renders into both the HTML body and the plain-text fallback.</summary>
    public required Func<IReadOnlyDictionary<string, string>, IReadOnlyList<string>> BuildParagraphs { get; init; }

    /// <summary>Key into the parameters dictionary holding a path relative to the site's origin
    /// (e.g. <c>/cabinet/orders/42</c>), or <c>null</c> when this notification has no follow-up page.
    /// Resolved against <c>NotificationOptions.SiteBaseUrl</c> — the template never builds the
    /// absolute URL itself.</summary>
    public string? LinkPathParameter { get; init; }

    /// <summary>Link text shown next to the URL. Required together with <see cref="LinkPathParameter"/>.</summary>
    public string? LinkLabel { get; init; }
}
