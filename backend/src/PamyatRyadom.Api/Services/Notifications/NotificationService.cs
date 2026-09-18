using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Services.Notifications;

/// <summary>
/// Transactional email on top of <see cref="IEmailSender"/>. Owns composing the message (subject,
/// HTML and text bodies, the shared signature and the link to whatever page the recipient needs
/// next) and sending it without letting a delivery failure propagate — the caller triggered a real
/// business event and that event already happened; a mail server being down does not undo it.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly INotificationTemplateCatalog _catalog;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEmailSender emailSender,
        INotificationTemplateCatalog catalog,
        IOptions<NotificationOptions> options,
        ILogger<NotificationService> logger)
    {
        _emailSender = emailSender;
        _catalog = catalog;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string eventType,
        string recipientEmail,
        string recipientRole,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        if (!_catalog.TryGet(eventType, out var template))
        {
            throw new ArgumentException($"No notification template is registered for event type '{eventType}'.", nameof(eventType));
        }

        if (template.RecipientRole != recipientRole)
        {
            throw new ArgumentException(
                $"Template '{eventType}' is written for recipient role '{template.RecipientRole}', not '{recipientRole}'.",
                nameof(recipientRole));
        }

        var subject = template.BuildSubject(parameters);
        var paragraphs = template.BuildParagraphs(parameters);
        var linkUrl = BuildLinkUrl(template, parameters);

        try
        {
            await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = recipientEmail,
                    Subject = subject,
                    HtmlBody = NotificationBodyRenderer.RenderHtml(paragraphs, linkUrl, template.LinkLabel),
                    TextBody = NotificationBodyRenderer.RenderText(paragraphs, linkUrl, template.LinkLabel),
                },
                ct);
            return true;
        }
        catch (Exception ex)
        {
            // Never log the recipient address or the rendered text — only identifiers.
            _logger.LogError(
                ex,
                "Failed to deliver a notification. EventType={EventType} RecipientKey={RecipientKey}",
                eventType,
                RecipientKey(recipientEmail));
            return false;
        }
    }

    private string? BuildLinkUrl(NotificationTemplate template, IReadOnlyDictionary<string, string> parameters)
    {
        if (template.LinkPathParameter is not { Length: > 0 } parameterName)
        {
            return null;
        }

        if (!parameters.TryGetValue(parameterName, out var path) || string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                $"Template '{template.EventType}' needs a '{parameterName}' parameter to build its link.",
                nameof(parameters));
        }

        return $"{_options.SiteBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    /// <summary>A stable, non-reversible key for correlating log lines about one recipient without
    /// putting the address itself in the log — same construction as <c>AuthService.DestinationKey</c>.</summary>
    private static string RecipientKey(string recipient) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recipient)))[..12].ToLowerInvariant();
}
