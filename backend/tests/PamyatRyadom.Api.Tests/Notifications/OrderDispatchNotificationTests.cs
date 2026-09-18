using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Dispatch;
using PamyatRyadom.Api.Models.Media;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.Payments;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Notifications;

/// <summary>
/// D19: the four events that actually call <c>INotificationService</c> — estimate published,
/// payment received, a visit offered to an executor, and QA approving the report. Each is a real
/// full-stack scenario (through HTTP, over the real order/payment/dispatch services) rather than a
/// unit test, because the point is that these services now call notifications at all, and only at
/// the right moment.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderDispatchNotificationTests : AuthIntegrationTest
{
    private const string ClientEmail = "notify-client@example.com";
    private const string DispatcherEmail = "notify-dispatcher@example.com";
    private const string ExecutorEmail = "notify-executor@example.com";
    private const string QaEmail = "notify-qa@example.com";

    public OrderDispatchNotificationTests(PostgresFixture postgres) : base(postgres) { }

    [Fact]
    public async Task Publishing_an_estimate_sends_exactly_one_client_email_with_no_payout()
    {
        var ctx = await ArrangeAsync();
        var before = ctx.Factory.Emails.SentTo(ClientEmail).Count;

        var created = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates", new
        {
            lines = new[] { new { type = "work", title = "Уборка участка", quantity = 1, unitPriceRub = 4900m } },
        });
        Assert.Equal(HttpStatusCode.Created, created.Status);

        var published = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates/1/publish");
        Assert.Equal(HttpStatusCode.OK, published.Status);

        var sent = ctx.Factory.Emails.SentTo(ClientEmail);
        Assert.Equal(before + 1, sent.Count);

        var notification = sent[^1];
        Assert.Contains(ctx.OrderNumber, notification.Subject);
        Assert.DoesNotContain("PayoutRub", notification.HtmlBody);
        Assert.DoesNotContain("PayoutRub", notification.TextBody);
    }

    [Fact]
    public async Task Publishing_an_estimate_twice_sends_a_client_email_each_time()
    {
        // BR-002: a correction supersedes the previous version, and the client needs to know a new
        // one is waiting — publishing is not a one-time event on an order.
        var ctx = await ArrangeAsync();

        await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates", new
        {
            lines = new[] { new { type = "work", title = "Уборка участка", quantity = 1, unitPriceRub = 4900m } },
        });
        await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates/1/publish");
        var afterFirst = ctx.Factory.Emails.SentTo(ClientEmail).Count;

        await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates", new
        {
            lines = new[] { new { type = "work", title = "Уборка (уточнено)", quantity = 1, unitPriceRub = 6400m } },
        });
        await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates/2/publish");

        Assert.Equal(afterFirst + 1, ctx.Factory.Emails.SentTo(ClientEmail).Count);
    }

    [Fact]
    public async Task A_successful_payment_sends_one_client_email_and_a_repeat_sync_sends_no_more()
    {
        var ctx = await ArrangeAsync();
        await PublishAndAcceptEstimateAsync(ctx);

        var before = ctx.Factory.Emails.SentTo(ClientEmail).Count;

        var started = await ctx.Client.PostAsync($"/api/v1/orders/{ctx.OrderId}/payments");
        Assert.Equal(HttpStatusCode.Created, started.Status);
        var paymentId = started.Data!["id"]!.GetValue<long>();

        var providerId = await ctx.Factory.QueryDbAsync(db => db.Payments
            .Where(p => p.Id == paymentId).Select(p => p.ProviderPaymentId).FirstAsync());
        ctx.Factory.Services.GetRequiredService<StubPaymentProvider>().ConfirmForTesting(providerId!);

        var firstSync = await ctx.Client.PostAsync($"/api/v1/payments/{paymentId}/sync");
        Assert.Equal(HttpStatusCode.OK, firstSync.Status);

        var afterFirstSync = ctx.Factory.Emails.SentTo(ClientEmail);
        Assert.Equal(before + 1, afterFirstSync.Count);
        Assert.Contains(ctx.OrderNumber, afterFirstSync[^1].Subject);

        // The client (or a retried webhook) can hit sync again on an already-succeeded payment —
        // that must not fire a second "payment received" email.
        var secondSync = await ctx.Client.PostAsync($"/api/v1/payments/{paymentId}/sync");
        Assert.Equal(HttpStatusCode.OK, secondSync.Status);
        Assert.Equal(before + 1, ctx.Factory.Emails.SentTo(ClientEmail).Count);
    }

    [Fact]
    public async Task Assigning_a_visit_sends_the_executor_exactly_one_email()
    {
        var ctx = await ArrangePaidOrderAsync();
        var before = ctx.Factory.Emails.SentTo(ExecutorEmail).Count;

        var offered = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/visits", new
        {
            executorUserId = ctx.ExecutorId,
            payoutRub = 1500m,
            offerHours = 24,
        });
        Assert.Equal(HttpStatusCode.Created, offered.Status);

        var sent = ctx.Factory.Emails.SentTo(ExecutorEmail);
        Assert.Equal(before + 1, sent.Count);

        var notification = sent[^1];
        Assert.Contains(ctx.OrderNumber, notification.Subject);
        // The address and checklist live behind the link, never inlined into the body.
        Assert.DoesNotContain("Хованское", notification.HtmlBody);
        Assert.DoesNotContain("Хованское", notification.TextBody);
    }

    [Fact]
    public async Task Qa_approval_sends_the_client_a_report_ready_email_with_no_payout()
    {
        var ctx = await ArrangePaidOrderAsync();

        var offered = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/visits", new
        {
            executorUserId = ctx.ExecutorId,
            payoutRub = 1500m,
        });
        var visitId = offered.Data!["id"]!.GetValue<long>();

        await ctx.Executor.PostAsync($"/api/v1/executor/visits/{visitId}/accept");
        await ctx.Executor.PostAsync($"/api/v1/executor/visits/{visitId}/start");
        await AddPhotoAsync(ctx.Factory, visitId, MediaPhases.Before);
        await AddPhotoAsync(ctx.Factory, visitId, MediaPhases.After);

        var filed = await ctx.Executor.PostAsync($"/api/v1/executor/visits/{visitId}/report", new
        {
            note = "Готово",
            checklist = await AnswersFor(ctx.Factory, visitId),
        });
        Assert.Equal(HttpStatusCode.OK, filed.Status);

        var before = ctx.Factory.Emails.SentTo(ClientEmail).Count;

        var approved = await ctx.Qa.PostAsync($"/api/v1/admin/visits/{visitId}/approve");
        Assert.Equal(HttpStatusCode.OK, approved.Status);

        var sent = ctx.Factory.Emails.SentTo(ClientEmail);
        Assert.Equal(before + 1, sent.Count);

        var notification = sent[^1];
        Assert.Contains(ctx.OrderNumber, notification.Subject);
        Assert.DoesNotContain("1500", notification.HtmlBody);
        Assert.DoesNotContain("1500", notification.TextBody);
    }

    // ---------------------------------------------------------------------------------------------

    private sealed record Context(
        PamyatApiFactory Factory, ApiClient Client, ApiClient Dispatcher, ApiClient Executor, ApiClient Qa,
        long OrderId, string OrderNumber, long ExecutorId);

    private async Task<Context> ArrangeAsync()
    {
        var factory = CreateFactory();

        long cemeteryId = 0;
        await factory.WithDbAsync(async db =>
        {
            var cemetery = new Cemetery { Name = "Хованское кладбище", Region = "Москва" };
            db.Cemeteries.Add(cemetery);

            if (!await db.ServicePackages.AnyAsync(p => p.Code == "basic"))
            {
                db.ServicePackages.Add(new ServicePackage
                {
                    Code = "basic",
                    Version = "1.0",
                    Title = "Базовый уход",
                    Summary = "Уборка участка",
                    PriceFromRub = 4_900m,
                    WarrantyDays = 14,
                    Status = ServicePackageStatuses.Published,
                    PublishedAt = DateTimeOffset.UtcNow,
                });
            }

            await db.SaveChangesAsync();
            cemeteryId = cemetery.Id;
        });

        var client = factory.CreateApiClient();
        await LoginAsync(factory, client, ClientEmail);

        var site = await client.PostAsync("/api/v1/burial-sites", new
        {
            cemeteryId,
            deceasedFullName = "Иванов Иван Иванович",
            plotSection = "уч. 12, ряд 3",
            landmarks = "третий ряд от часовни, синяя ограда",
        });

        var order = await client.PostAsync("/api/v1/orders", new
        {
            burialSiteId = site.Data!["id"]!.GetValue<long>(),
            packageCode = "basic",
        });

        var orderId = order.Data!["id"]!.GetValue<long>();
        var orderNumber = order.Data["number"]!.GetValue<string>();
        await client.PostAsync($"/api/v1/orders/{orderId}/submit");

        var dispatcher = factory.CreateApiClient();
        await LoginAsync(factory, dispatcher, DispatcherEmail);
        await SetRoleAsync(factory, DispatcherEmail, UserRoles.Dispatcher);

        var executor = factory.CreateApiClient();
        await LoginAsync(factory, executor, ExecutorEmail);
        var executorId = await SetRoleAsync(factory, ExecutorEmail, UserRoles.Executor);

        var qa = factory.CreateApiClient();
        await LoginAsync(factory, qa, QaEmail);
        await SetRoleAsync(factory, QaEmail, UserRoles.Qa);

        return new Context(factory, client, dispatcher, executor, qa, orderId, orderNumber, executorId);
    }

    private static async Task PublishAndAcceptEstimateAsync(Context ctx)
    {
        await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates", new
        {
            lines = new[] { new { type = "work", title = "Уборка участка", quantity = 1, unitPriceRub = 4900m } },
        });
        await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/estimates/1/publish");
        await ctx.Client.PostAsync($"/api/v1/orders/{ctx.OrderId}/estimate-acceptance", new { version = 1 });
    }

    private async Task<Context> ArrangePaidOrderAsync()
    {
        var ctx = await ArrangeAsync();
        await PublishAndAcceptEstimateAsync(ctx);

        var started = await ctx.Client.PostAsync($"/api/v1/orders/{ctx.OrderId}/payments");
        Assert.Equal(HttpStatusCode.Created, started.Status);

        var paymentId = started.Data!["id"]!.GetValue<long>();
        var providerId = await ctx.Factory.QueryDbAsync(db => db.Payments
            .Where(p => p.Id == paymentId).Select(p => p.ProviderPaymentId).FirstAsync());

        ctx.Factory.Services.GetRequiredService<StubPaymentProvider>().ConfirmForTesting(providerId!);

        var synced = await ctx.Client.PostAsync($"/api/v1/payments/{paymentId}/sync");
        Assert.Equal(HttpStatusCode.OK, synced.Status);

        return ctx;
    }

    private static async Task<object[]> AnswersFor(PamyatApiFactory factory, long visitId)
    {
        var keys = await factory.QueryDbAsync(db => db.VisitChecklistItems
            .Where(i => i.VisitId == visitId).Select(i => i.Key).ToListAsync());

        return keys.Select(object (k) => new { key = k, result = ChecklistResults.Done }).ToArray();
    }

    private static Task AddPhotoAsync(PamyatApiFactory factory, long visitId, string phase) =>
        factory.WithDbAsync(async db =>
        {
            db.MediaAssets.Add(new MediaAsset
            {
                OwnerType = MediaOwnerTypes.Visit,
                OwnerId = visitId,
                Phase = phase,
                Status = MediaStatuses.Ready,
                ReadyAt = DateTimeOffset.UtcNow,
                StorageKey = $"visits/{visitId}/{phase}-{Guid.NewGuid():N}.webp",
                ContentType = "image/webp",
                FileSizeBytes = 1024,
                UploadedByUserId = null,
            });

            await db.SaveChangesAsync();
        });

    private static async Task<long> SetRoleAsync(PamyatApiFactory factory, string email, string role)
    {
        long id = 0;

        await factory.WithDbAsync(async db =>
        {
            var identity = await db.AuthIdentities.Include(i => i.User).FirstAsync(i => i.Email == email);
            identity.User!.Role = role;
            id = identity.User.Id;
            await db.SaveChangesAsync();
        });

        return id;
    }
}
