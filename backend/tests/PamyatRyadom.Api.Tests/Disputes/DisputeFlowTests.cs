using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Disputes;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.Orders;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Disputes;

/// <summary>
/// The case behind an order's `disputed` status: opening it, and the two ways it must refuse to.
///
/// These tests fast-forward an order to a target status through <see cref="IOrderService"/>
/// directly (resolved from DI, not through HTTP) rather than replaying payment and dispatch —
/// that machinery is already exercised end to end in <c>VisitFlowTests</c>, and none of it
/// changes what <c>DisputeService</c> does once the order gets there.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DisputeFlowTests : AuthIntegrationTest
{
    private const string ClientEmail = "dispute-client@example.com";

    /// <summary>The ordinary path's steps from a freshly created order (`draft`) to
    /// `customer_review`, in order.</summary>
    private static readonly string[] ToCustomerReview =
    {
        OrderStatuses.Submitted, OrderStatuses.EstimateReady, OrderStatuses.AwaitingPayment,
        OrderStatuses.Paid, OrderStatuses.Assigning, OrderStatuses.Assigned,
        OrderStatuses.InProgress, OrderStatuses.QaReview, OrderStatuses.CustomerReview,
    };

    /// <summary>The same path, stopping one short — while QA still has the report.</summary>
    private static readonly string[] ToQaReview = ToCustomerReview[..^1];

    public DisputeFlowTests(PostgresFixture postgres) : base(postgres) { }

    [Fact]
    public async Task A_client_opens_a_dispute_from_customer_review_and_sees_its_state()
    {
        var (factory, client, orderId) = await ArrangeOrderAsync();
        await MoveOrderAsync(factory, orderId, ToCustomerReview);

        var opened = await client.PostAsync($"/api/v1/orders/{orderId}/dispute", new
        {
            reason = "Памятник не помыли, цветы не поменяли.",
        });

        Assert.Equal(HttpStatusCode.Created, opened.Status);
        Assert.Equal("Памятник не помыли, цветы не поменяли.", opened.Data?["reason"]?.GetValue<string>());
        Assert.Equal(DisputeStatuses.Open, opened.Data?["status"]?.GetValue<string>());
        Assert.Null(opened.Data?["resolutionType"]);

        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.Disputed);

        var seen = await client.GetAsync($"/api/v1/orders/{orderId}/dispute");
        Assert.Equal(HttpStatusCode.OK, seen.Status);
        Assert.Equal(DisputeStatuses.Open, seen.Data?["status"]?.GetValue<string>());
        Assert.Equal("Памятник не помыли, цветы не поменяли.", seen.Data?["reason"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_dispute_with_no_reason_is_refused()
    {
        var (factory, client, orderId) = await ArrangeOrderAsync();
        await MoveOrderAsync(factory, orderId, ToCustomerReview);

        var blank = await client.PostAsync($"/api/v1/orders/{orderId}/dispute", new { reason = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, blank.Status);
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.CustomerReview);
    }

    [Fact]
    public async Task A_dispute_cannot_be_opened_while_qa_is_still_reviewing_the_report()
    {
        var (factory, client, orderId) = await ArrangeOrderAsync();
        await MoveOrderAsync(factory, orderId, ToQaReview);

        var attempt = await client.PostAsync($"/api/v1/orders/{orderId}/dispute", new
        {
            reason = "Пусть проверят получше.",
        });

        // The client cannot see the report yet (BR-010), so there is nothing to have an opinion
        // about — the state machine refuses the transition before a case is ever created.
        Assert.Equal(HttpStatusCode.BadRequest, attempt.Status);
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.QaReview);

        var noCase = await client.GetAsync($"/api/v1/orders/{orderId}/dispute");
        Assert.Equal(HttpStatusCode.NotFound, noCase.Status);
    }

    [Fact]
    public async Task A_second_live_dispute_on_the_same_order_is_refused()
    {
        var (factory, client, orderId) = await ArrangeOrderAsync();
        await MoveOrderAsync(factory, orderId, ToCustomerReview);

        var first = await client.PostAsync($"/api/v1/orders/{orderId}/dispute", new { reason = "Не устраивает качество." });
        Assert.Equal(HttpStatusCode.Created, first.Status);

        // Nothing has resolved the first case yet (D15 is not built), but the order is moved on
        // to `completed` anyway — the exact situation the "one live dispute" rule exists for.
        // `completed` is itself a status a client may dispute from (the warranty window, BR-012),
        // so the state machine alone would allow a second transition here; only the dispute's own
        // "already have a live case" check stands in the way.
        await MoveOrderAsync(factory, orderId, new[] { OrderStatuses.Completed });

        var second = await client.PostAsync($"/api/v1/orders/{orderId}/dispute", new { reason = "И ещё вот это." });

        Assert.Equal(HttpStatusCode.BadRequest, second.Status);
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.Completed);

        var disputeCount = await factory.QueryDbAsync(db => db.Disputes.CountAsync(d => d.OrderId == orderId));
        Assert.Equal(1, disputeCount);
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<(PamyatApiFactory Factory, ApiClient Client, long OrderId)> ArrangeOrderAsync()
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

        return (factory, client, order.Data!["id"]!.GetValue<long>());
    }

    /// <summary>Drives an order through <paramref name="steps"/> via the real state machine,
    /// skipping the payment/dispatch/QA machinery that ordinarily earns each step — that path is
    /// covered elsewhere, and none of it changes what a dispute does once the order arrives.</summary>
    private static async Task MoveOrderAsync(PamyatApiFactory factory, long orderId, IReadOnlyList<string> steps)
    {
        using var scope = factory.Services.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        foreach (var step in steps)
        {
            var moved = await orders.TransitionAsync(orderId, step, null, "test", "test-fast-forward");
            Assert.True(moved.Succeeded, $"→ {step}: {string.Join(", ", moved.Errors.Select(e => e.Message))}");
        }
    }

    private static async Task AssertOrderStatusAsync(PamyatApiFactory factory, long orderId, string expected)
    {
        var actual = await factory.QueryDbAsync(db => db.Orders
            .Where(o => o.Id == orderId).Select(o => o.Status).FirstAsync());

        Assert.Equal(expected, actual);
    }
}
