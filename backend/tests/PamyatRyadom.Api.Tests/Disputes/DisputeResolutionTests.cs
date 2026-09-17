using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Disputes;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.Orders;
using PamyatRyadom.Api.Services.Payments;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Disputes;

/// <summary>
/// Working a dispute from the support/finance/admin side (D15): claiming it, resolving it without
/// money (rework/reject) and resolving it with a refund.
///
/// A real payment is arranged here — unlike <see cref="DisputeFlowTests"/>, which fast-forwards
/// the order status directly and never touches Payments — because a refund needs an actual
/// succeeded <c>Payment</c> row to act on. Everything between "paid" and "customer_review" is
/// still fast-forwarded through <see cref="IOrderService"/> directly: that machinery (dispatch,
/// the visit, QA) is exercised end to end in <c>VisitFlowTests</c> and none of it changes what a
/// dispute resolution does once the order arrives.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DisputeResolutionTests : AuthIntegrationTest
{
    private const string ClientEmail = "dispute-resolution-client@example.com";
    private const string DispatcherEmail = "dispute-resolution-dispatcher@example.com";
    private const string SupportEmail = "dispute-resolution-support@example.com";
    private const string FinanceEmail = "dispute-resolution-finance@example.com";

    private const decimal PriceRub = 4900m;

    public DisputeResolutionTests(PostgresFixture postgres) : base(postgres) { }

    [Fact]
    public async Task Support_cannot_perform_a_refund_directly()
    {
        var (factory, disputeId, _) = await ArrangeDisputedPaidOrderAsync();

        var support = factory.CreateApiClient();
        await LoginAsync(factory, support, SupportEmail);
        await SetRoleAsync(factory, SupportEmail, UserRoles.Support);

        var attempt = await support.PostAsync($"/api/v1/admin/disputes/{disputeId}/refund", new
        {
            resolutionText = "Возврат за некачественную уборку.",
        });

        Assert.Equal(HttpStatusCode.Forbidden, attempt.Status);
    }

    [Fact]
    public async Task A_full_refund_moves_the_order_to_refunded()
    {
        var (factory, disputeId, orderId) = await ArrangeDisputedPaidOrderAsync();

        var finance = factory.CreateApiClient();
        await LoginAsync(factory, finance, FinanceEmail);
        await SetRoleAsync(factory, FinanceEmail, UserRoles.Finance);

        var resolved = await finance.PostAsync($"/api/v1/admin/disputes/{disputeId}/refund", new
        {
            resolutionText = "Работу не переделать — возвращаем деньги целиком.",
        });

        Assert.Equal(HttpStatusCode.OK, resolved.Status);
        Assert.Equal(DisputeStatuses.Resolved, resolved.Data?["status"]?.GetValue<string>());
        Assert.Equal(DisputeResolutionTypes.FullRefund, resolved.Data?["resolutionType"]?.GetValue<string>());
        Assert.Null(resolved.Data?["refundAmountRub"]);

        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.Refunded);
    }

    [Fact]
    public async Task A_partial_refund_leaves_the_order_where_it_was()
    {
        var (factory, disputeId, orderId) = await ArrangeDisputedPaidOrderAsync();

        var finance = factory.CreateApiClient();
        await LoginAsync(factory, finance, FinanceEmail);
        await SetRoleAsync(factory, FinanceEmail, UserRoles.Finance);

        var resolved = await finance.PostAsync($"/api/v1/admin/disputes/{disputeId}/refund", new
        {
            amountRub = 2000m,
            resolutionText = "Цветы не поменяли — частичный возврат за эту часть работы.",
        });

        Assert.Equal(HttpStatusCode.OK, resolved.Status);
        Assert.Equal(DisputeStatuses.Resolved, resolved.Data?["status"]?.GetValue<string>());
        Assert.Equal(DisputeResolutionTypes.PartialRefund, resolved.Data?["resolutionType"]?.GetValue<string>());
        Assert.Equal(2000m, resolved.Data?["refundAmountRub"]?.GetValue<decimal>());

        // The work still happened — a partial refund is a price correction, not a reversal.
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.Completed);
    }

    [Fact]
    public async Task Rework_sends_the_order_back_to_in_progress()
    {
        var (factory, disputeId, orderId) = await ArrangeDisputedPaidOrderAsync();

        var support = factory.CreateApiClient();
        await LoginAsync(factory, support, SupportEmail);
        await SetRoleAsync(factory, SupportEmail, UserRoles.Support);

        var claimed = await support.PostAsync($"/api/v1/admin/disputes/{disputeId}/claim", new { });
        Assert.Equal(HttpStatusCode.OK, claimed.Status);
        Assert.Equal(DisputeStatuses.InReview, claimed.Data?["status"]?.GetValue<string>());

        var resolved = await support.PostAsync($"/api/v1/admin/disputes/{disputeId}/resolve", new
        {
            resolutionType = DisputeResolutionTypes.Rework,
            resolutionText = "Отправляем исполнителя переделать участок.",
        });

        Assert.Equal(HttpStatusCode.OK, resolved.Status);
        Assert.Equal(DisputeStatuses.Resolved, resolved.Data?["status"]?.GetValue<string>());
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.InProgress);
    }

    [Fact]
    public async Task A_resolution_with_no_text_is_refused()
    {
        var (factory, disputeId, orderId) = await ArrangeDisputedPaidOrderAsync();

        var admin = factory.CreateApiClient();
        await LoginAsync(factory, admin, "dispute-resolution-admin@example.com");
        await SetRoleAsync(factory, "dispute-resolution-admin@example.com", UserRoles.Admin);

        var attempt = await admin.PostAsync($"/api/v1/admin/disputes/{disputeId}/resolve", new
        {
            resolutionType = DisputeResolutionTypes.Rejected,
            resolutionText = "   ",
        });

        Assert.Equal(HttpStatusCode.BadRequest, attempt.Status);
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.Disputed);
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<(PamyatApiFactory Factory, long DisputeId, long OrderId)> ArrangeDisputedPaidOrderAsync()
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
                    PriceFromRub = PriceRub,
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
            lines = new[] { new { type = "work", title = "Уборка участка", quantity = 1, unitPriceRub = PriceRub } },
        });
        await dispatcher.PostAsync($"/api/v1/admin/orders/{orderId}/estimates/1/publish");
        await client.PostAsync($"/api/v1/orders/{orderId}/estimate-acceptance", new { version = 1 });

        var started = await client.PostAsync($"/api/v1/orders/{orderId}/payments");
        var paymentId = started.Data!["id"]!.GetValue<long>();
        var providerId = await factory.QueryDbAsync(db => db.Payments
            .Where(p => p.Id == paymentId).Select(p => p.ProviderPaymentId).FirstAsync());

        factory.Services.GetRequiredService<StubPaymentProvider>().ConfirmForTesting(providerId!);
        await client.PostAsync($"/api/v1/payments/{paymentId}/sync");
        await AssertOrderStatusAsync(factory, orderId, OrderStatuses.Paid);

        // Dispatch, the visit and QA are exercised end to end in VisitFlowTests — fast-forwarding
        // here keeps this file about dispute resolution, not about re-proving that path.
        await MoveOrderAsync(factory, orderId, new[]
        {
            OrderStatuses.Assigning, OrderStatuses.Assigned, OrderStatuses.InProgress,
            OrderStatuses.QaReview, OrderStatuses.CustomerReview,
        });

        var opened = await client.PostAsync($"/api/v1/orders/{orderId}/dispute", new
        {
            reason = "Памятник не помыли, цветы не поменяли.",
        });
        Assert.Equal(HttpStatusCode.Created, opened.Status);

        return (factory, opened.Data!["id"]!.GetValue<long>(), orderId);
    }

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

    private static async Task SetRoleAsync(PamyatApiFactory factory, string email, string role)
    {
        await factory.WithDbAsync(async db =>
        {
            var identity = await db.AuthIdentities.Include(i => i.User).FirstAsync(i => i.Email == email);
            identity.User!.Role = role;
            await db.SaveChangesAsync();
        });
    }

    private static async Task AssertOrderStatusAsync(PamyatApiFactory factory, long orderId, string expected)
    {
        var actual = await factory.QueryDbAsync(db => db.Orders
            .Where(o => o.Id == orderId).Select(o => o.Status).FirstAsync());

        Assert.Equal(expected, actual);
    }
}
