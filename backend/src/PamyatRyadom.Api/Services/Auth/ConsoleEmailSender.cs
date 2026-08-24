namespace PamyatRyadom.Api.Services.Auth;

/// <summary>Development-only sender: logs the message instead of sending it, so OTP codes are visible
/// in the console/log stream during local dev. Registered only when
/// builder.Environment.IsDevelopment() (see Program.cs) — a clean DI swap for SmtpEmailSender, not a
/// runtime "expose the code in the API response" flag (NaidiAI's AUTH_EXPOSE_DEV_CODE hack, which this
/// deliberately does not replicate: a misconfigured ASPNETCORE_ENVIRONMENT could never leak a code
/// through this API response the way it could through that one).</summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DEV EMAIL] To={To} Subject={Subject}\n{Body}",
            message.To,
            message.Subject,
            message.TextBody ?? message.HtmlBody);
        return Task.CompletedTask;
    }
}
