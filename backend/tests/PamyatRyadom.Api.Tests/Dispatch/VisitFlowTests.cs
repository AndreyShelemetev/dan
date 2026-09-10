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

namespace PamyatRyadom.Api.Tests.Dispatch;

/// <summary>
/// The half of the product that happens after the money: dispatch, the visit, the photo report,
/// QA, and the client accepting the work.
///
/// The invariants under test are the promises the service is sold on. A client never sees a
/// report QA has not passed. A report without a "before" and an "after" is not a report. And the
/// executor's payout never reaches anything the client can read.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class VisitFlowTests : AuthIntegrationTest
{
    private const string ClientEmail = "visit-client@example.com";
    private const string DispatcherEmail = "visit-dispatcher@example.com";
    private const string ExecutorEmail = "visit-executor@example.com";
    private const string QaEmail = "visit-qa@example.com";

    public VisitFlowTests(PostgresFixture postgres) : base(postgres) { }

    [Fact]
    public async Task An_order_travels_from_payment_to_an_accepted_photo_report()
    {
        var ctx = await ArrangeePaidOrderAsync();

        // Dispatch offers the work. The client never picks an executor — this is a managed
        // service, and the assignment is ours to make.
        var offered = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/visits", new
        {
            executorUserId = ctx.ExecutorId,
            payoutRub = 1500m,
            offerHours = 24,
        });

        Assert.Equal(HttpStatusCode.Created, offered.Status);
        var visitId = offered.Data!["id"]!.GetValue<long>();
        Assert.Equal(VisitStatuses.Offered, offered.Data["status"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.OK, (await ctx.Executor.PostAsync($"/api/v1/executor/visits/{visitId}/accept")).Status);
        Assert.Equal(HttpStatusCode.OK, (await ctx.Executor.PostAsync($"/api/v1/executor/visits/{visitId}/start")).Status);

        await AssertOrderStatusAsync(ctx.Factory, ctx.OrderId, OrderStatuses.InProgress);

        // A report with no photographs is refused. The photographs are the deliverable, not an
        // attachment to it.
        var empty = await ctx.Executor.PostAsync($"/api/v1/executor/visits/{visitId}/report", new
        {
            note = "Убрано",
            checklist = await AnswersFor(ctx.Factory, visitId),
        });

        Assert.Equal(HttpStatusCode.BadRequest, empty.Status);

        await AddPhotoAsync(ctx.Factory, visitId, MediaPhases.Before);
        await AddPhotoAsync(ctx.Factory, visitId, MediaPhases.After);

        var filed = await ctx.Executor.PostAsync($"/api/v1/executor/visits/{visitId}/report", new
        {
            note = "Убрали листья, помыли памятник, поставили свежие цветы.",
            checklist = await AnswersFor(ctx.Factory, visitId),
        });

        Assert.Equal(HttpStatusCode.OK, filed.Status);
        await AssertOrderStatusAsync(ctx.Factory, ctx.OrderId, OrderStatuses.QaReview);

        // Before QA passes it, the client has nothing to see.
        Assert.Equal(HttpStatusCode.NotFound, (await ctx.Client.GetAsync($"/api/v1/orders/{ctx.OrderId}/report")).Status);

        Assert.Equal(HttpStatusCode.OK, (await ctx.Qa.PostAsync($"/api/v1/admin/visits/{visitId}/approve")).Status);
        await AssertOrderStatusAsync(ctx.Factory, ctx.OrderId, OrderStatuses.CustomerReview);

        var report = await ctx.Client.GetAsync($"/api/v1/orders/{ctx.OrderId}/report");
        Assert.Equal(HttpStatusCode.OK, report.Status);
        Assert.Equal(
            "Убрали листья, помыли памятник, поставили свежие цветы.",
            report.Data?["note"]?.GetValue<string>());

        // The payout is commercially confidential. Not filtered out of the client's view — absent
        // from the shape entirely, so no future field can reintroduce it.
        Assert.Null(report.Data?["payoutRub"]);
        Assert.DoesNotContain("1500", report.Raw);

        var accepted = await ctx.Client.PostAsync($"/api/v1/orders/{ctx.OrderId}/acceptance");
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        await AssertOrderStatusAsync(ctx.Factory, ctx.OrderId, OrderStatuses.Completed);
    }

    [Fact]
    public async Task Qa_sends_a_report_back_with_a_reason_and_never_without_one()
    {
        var ctx = await ArrangeePaidOrderAsync();
        var visitId = await FileAReportAsync(ctx);

        var silent = await ctx.Qa.PostAsync($"/api/v1/admin/visits/{visitId}/rework", new { note = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, silent.Status);

        var sentBack = await ctx.Qa.PostAsync($"/api/v1/admin/visits/{visitId}/rework", new
        {
            note = "На фото «после» не видно всей плиты — переснимите с того же ракурса, что и «до».",
        });

        Assert.Equal(HttpStatusCode.OK, sentBack.Status);
        Assert.Equal(VisitStatuses.Rework, sentBack.Data?["status"]?.GetValue<string>());
        await AssertOrderStatusAsync(ctx.Factory, ctx.OrderId, OrderStatuses.InProgress);

        // The executor can re-file; the client still sees nothing.
        Assert.Equal(HttpStatusCode.NotFound, (await ctx.Client.GetAsync($"/api/v1/orders/{ctx.OrderId}/report")).Status);
    }

    [Fact]
    public async Task An_executor_cannot_touch_a_visit_that_is_not_theirs()
    {
        var ctx = await ArrangeePaidOrderAsync();

        var offered = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/visits", new
        {
            executorUserId = ctx.ExecutorId,
            payoutRub = 1500m,
        });

        var visitId = offered.Data!["id"]!.GetValue<long>();

        var stranger = ctx.Factory.CreateApiClient();
        await LoginAsync(ctx.Factory, stranger, "visit-other-executor@example.com");
        await SetRoleAsync(ctx.Factory, "visit-other-executor@example.com", UserRoles.Executor);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/executor/visits/{visitId}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/v1/executor/visits/{visitId}/accept")).Status);
    }

    [Fact]
    public async Task A_second_executor_cannot_be_sent_to_the_same_grave()
    {
        var ctx = await ArrangeePaidOrderAsync();

        var first = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/visits", new
        {
            executorUserId = ctx.ExecutorId,
        });

        Assert.Equal(HttpStatusCode.Created, first.Status);

        var second = await ctx.Dispatcher.PostAsync($"/api/v1/admin/orders/{ctx.OrderId}/visits", new
        {
            executorUserId = ctx.ExecutorId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.Status);
    }

    // ---------------------------------------------------------------------------------------------

    private sealed record Context(
        PamyatApiFactory Factory, ApiClient Client, ApiClient Dispatcher, ApiClient Executor,
        ApiClient Qa, long OrderId, long ExecutorId);

    /// <summary>An order carried all the way to paid, through the real payment path: the stub
    /// provider confirms, the application re-reads it, and that is what moves the order.</summary>
    private async Task<Context> ArrangeePaidOrderAsync()
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
        await client.PostAsync($"/api/v1/orders/{orderId}/submit");

        var dispatcher = factory.CreateApiClient();
        await LoginAsync(factory, dispatcher, DispatcherEmail);
        await SetRoleAsync(factory, DispatcherEmail, UserRoles.Dispatcher);

        await dispatcher.PostAsync($"/api/v1/admin/orders/{orderId}/estimates", new
        {
            lines = new[] { new { type = "work", title = "Уборка участка", quantity = 1, unitPriceRub = 4900m } },
        });
        await dispatcher.PostAsync($"/api/v1/admin/orders/{orderId}/estimates/1/publish");
        await client.PostAsync($"/api/v1/orders/{orderId}/estimate-acceptance", new { version = 1 });

        var started = await client.PostAsync($"/api/v1/orders/{orderId}/payments");
        Assert.Equal(HttpStatusCode.Created, started.Status);

        var paymentId = started.Data!["id"]!.GetValue<long>();
        var providerId = await factory.QueryDbAsync(db => db.Payments
            .Where(p => p.Id == paymentId).Select(p => p.ProviderPaymentId).FirstAsync());

        factory.Services.GetRequiredService<StubPaymentProvider>().ConfirmForTesting(providerId!);

        var synced = await client.PostAsync($"/api/v1/payments/{paymentId}/sync");
        Assert.Equal(HttpStatusCode.OK, synced.Status);
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.Paid);

        var executor = factory.CreateApiClient();
        await LoginAsync(factory, executor, ExecutorEmail);
        var executorId = await SetRoleAsync(factory, ExecutorEmail, UserRoles.Executor);

        var qa = factory.CreateApiClient();
        await LoginAsync(factory, qa, QaEmail);
        await SetRoleAsync(factory, QaEmail, UserRoles.Qa);

        return new Context(factory, client, dispatcher, executor, qa, orderId, executorId);
    }

    private async Task<long> FileAReportAsync(Context ctx)
    {
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
        return visitId;
    }

    /// <summary>Marks every checklist line done. The rules about answers that need a reason have
    /// their own tests; here the checklist is a precondition, not the subject.</summary>
    private static async Task<object[]> AnswersFor(PamyatApiFactory factory, long visitId)
    {
        var keys = await factory.QueryDbAsync(db => db.VisitChecklistItems
            .Where(i => i.VisitId == visitId).Select(i => i.Key).ToListAsync());

        return keys.Select(object (k) => new { key = k, result = ChecklistResults.Done }).ToArray();
    }

    /// <summary>Puts a ready photograph in the bucket record. The upload pipeline has its own
    /// tests; what matters here is that the report checks for one.</summary>
    private static Task AddPhotoAsync(PamyatApiFactory factory, long visitId, string phase) =>
        factory.WithDbAsync(async db =>
        {
            db.MediaAssets.Add(new MediaAsset
            {
                OwnerType = MediaOwnerTypes.Visit,
                OwnerId = visitId,
                Phase = phase,
                Status = MediaStatuses.Ready,
                // The schema requires it of anything ready — a photo that passed checks knows when.
                ReadyAt = DateTimeOffset.UtcNow,
                StorageKey = $"visits/{visitId}/{phase}-{Guid.NewGuid():N}.webp",
                ContentType = "image/webp",
                FileSizeBytes = 1024,
                UploadedByUserId = null,
            });

            await db.SaveChangesAsync();
        });

    private static async Task AssertOrderStatusAsync(PamyatApiFactory factory, long orderId, string expected)
    {
        var actual = await factory.QueryDbAsync(db => db.Orders
            .Where(o => o.Id == orderId).Select(o => o.Status).FirstAsync());

        Assert.Equal(expected, actual);
    }

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
