using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="IEmailSender"/>. The API picks its sender by environment
/// (<c>ConsoleEmailSender</c> in Development, <c>SmtpEmailSender</c> everywhere else, Program.cs),
/// and the test host runs as "Testing" — so without this replacement every OTP test would try to
/// open an SMTP connection. It records what was "sent" so a test can read the login code the same
/// way a user would, instead of reaching into the database (where only the PBKDF2 hash lives).
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private static readonly Regex SixDigits = new(@"\b\d{6}\b", RegexOptions.Compiled);

    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    public IReadOnlyList<EmailMessage> SentTo(string destination) =>
        _sent.Where(x => string.Equals(x.To, destination, StringComparison.OrdinalIgnoreCase)).ToArray();

    public EmailMessage? LastMessageTo(string destination) => SentTo(destination).LastOrDefault();

    /// <summary>The most recent login code sent to <paramref name="destination"/>, or null if none
    /// was sent. Read out of the plain-text body, i.e. exactly what the recipient sees.</summary>
    public string? LastCodeTo(string destination)
    {
        var body = LastMessageTo(destination)?.TextBody;
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        var match = SixDigits.Match(body);
        return match.Success ? match.Value : null;
    }

    /// <summary>The last code sent, asserted to exist — the common case in a login test.</summary>
    public string RequireLastCodeTo(string destination) =>
        LastCodeTo(destination)
        ?? throw new InvalidOperationException($"No login code was sent to '{destination}'.");
}
