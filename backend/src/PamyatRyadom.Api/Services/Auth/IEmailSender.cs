namespace PamyatRyadom.Api.Services.Auth;

/// <summary>A transactional email to send. Bodies are pre-rendered by the caller.</summary>
public sealed class EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public string? TextBody { get; init; }
}

/// <summary>Sends transactional email. Throws on a real send failure.
/// Two implementations, swapped by environment in Program.cs (not a runtime flag):
/// <see cref="ConsoleEmailSender"/> in Development, <see cref="SmtpEmailSender"/> everywhere else.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
