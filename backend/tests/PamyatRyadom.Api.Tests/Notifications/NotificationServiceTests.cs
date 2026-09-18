using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Notifications;

namespace PamyatRyadom.Api.Tests.Notifications;

/// <summary>
/// The notification service against fake templates and a fake mail transport — no SMTP, no
/// Postgres, no Docker. What matters here: a template renders into a subject/HTML/text triple with
/// the shared signature and link, a delivery failure never throws and never logs the address, and a
/// programmer error (unknown event, wrong recipient role) fails loudly instead of silently mis-sending.
/// </summary>
public sealed class NotificationServiceTests
{
    private const string ClientEventType = "test_client_event";
    private const string Recipient = "client@example.com";

    [Fact]
    public async Task SendAsync_renders_the_template_and_delivers_it()
    {
        var emailSender = new FakeEmailSender();
        var service = CreateService(emailSender, out _);

        var sent = await service.SendAsync(
            ClientEventType,
            Recipient,
            NotificationRecipientRoles.Client,
            new Dictionary<string, string> { ["orderNumber"] = "42", ["path"] = "/cabinet/orders/42" });

        Assert.True(sent);
        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(Recipient, message.To);
        Assert.Equal("Заказ 42 обновлён", message.Subject);

        Assert.Contains("По заказу 42 есть обновление.", message.HtmlBody);
        Assert.Contains("http://localhost:3100/cabinet/orders/42", message.HtmlBody);
        // The signature's guillemets are HTML-encoded (&#171;/&#187;) by the same escaping that
        // protects every other piece of template text, so the raw quote marks won't appear here.
        Assert.Contains("Память рядом", message.HtmlBody);

        Assert.Contains("http://localhost:3100/cabinet/orders/42", message.TextBody);
        Assert.Contains("«Память рядом»", message.TextBody!);
    }

    [Fact]
    public async Task A_delivery_failure_does_not_throw_and_never_logs_the_address()
    {
        var emailSender = new FakeEmailSender(throwOnSend: true);
        var service = CreateService(emailSender, out var logger);

        var sent = await service.SendAsync(
            ClientEventType,
            Recipient,
            NotificationRecipientRoles.Client,
            new Dictionary<string, string> { ["orderNumber"] = "42", ["path"] = "/cabinet/orders/42" });

        Assert.False(sent);

        var logged = Assert.Single(logger.Messages);
        Assert.Contains(ClientEventType, logged);
        Assert.DoesNotContain(Recipient, logged);
        Assert.DoesNotContain("@example.com", logged);
    }

    [Fact]
    public async Task Refuses_an_unregistered_event_type()
    {
        var service = CreateService(new FakeEmailSender(), out _);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(
            "no_such_event",
            Recipient,
            NotificationRecipientRoles.Client,
            new Dictionary<string, string>()));
    }

    [Fact]
    public async Task Refuses_to_send_a_client_template_to_an_executor()
    {
        var service = CreateService(new FakeEmailSender(), out _);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(
            ClientEventType,
            Recipient,
            NotificationRecipientRoles.Executor,
            new Dictionary<string, string> { ["orderNumber"] = "42", ["path"] = "/cabinet/orders/42" }));
    }

    [Fact]
    public async Task Refuses_to_build_a_link_when_the_path_parameter_is_missing()
    {
        var service = CreateService(new FakeEmailSender(), out _);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(
            ClientEventType,
            Recipient,
            NotificationRecipientRoles.Client,
            new Dictionary<string, string> { ["orderNumber"] = "42" }));
    }

    private static NotificationService CreateService(FakeEmailSender emailSender, out ListLogger<NotificationService> logger)
    {
        logger = new ListLogger<NotificationService>();
        return new NotificationService(
            emailSender,
            new FakeCatalog(),
            Options.Create(new NotificationOptions()),
            logger);
    }

    /// <summary>One template registered directly, bypassing the (empty, at this stage)
    /// <see cref="NotificationTemplateCatalog"/> — the events themselves are wired up by later tasks.</summary>
    private sealed class FakeCatalog : INotificationTemplateCatalog
    {
        private static readonly NotificationTemplate ClientTemplate = new()
        {
            EventType = ClientEventType,
            RecipientRole = NotificationRecipientRoles.Client,
            BuildSubject = p => $"Заказ {p["orderNumber"]} обновлён",
            BuildParagraphs = p => new[] { $"По заказу {p["orderNumber"]} есть обновление." },
            LinkPathParameter = "path",
            LinkLabel = "Открыть заказ",
        };

        public bool TryGet(string eventType, out NotificationTemplate template)
        {
            if (eventType == ClientEventType)
            {
                template = ClientTemplate;
                return true;
            }

            template = null!;
            return false;
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly bool _throwOnSend;

        public FakeEmailSender(bool throwOnSend = false)
        {
            _throwOnSend = throwOnSend;
        }

        public List<EmailMessage> Sent { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (_throwOnSend)
            {
                throw new InvalidOperationException("SMTP host unreachable.");
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Records every formatted log line, so a test can assert what did — and did not —
    /// end up in the log, without a real logging provider.</summary>
    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
