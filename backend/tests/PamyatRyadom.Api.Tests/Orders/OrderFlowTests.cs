using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Orders;

/// <summary>
/// The order path end to end, through HTTP.
///
/// The money invariants are what these are really for: an order cannot be paid for a quote
/// nobody agreed to, a correction invalidates the previous agreement, and one client cannot see
/// another's order.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderFlowTests : AuthIntegrationTest
{
    private const string ClientEmail = "order-client@example.com";
    private const string StrangerEmail = "order-stranger@example.com";
    private const string DispatcherEmail = "order-dispatcher@example.com";

    private const string OrdersPath = "/api/v1/orders";
    private const string AdminOrdersPath = "/api/v1/admin/orders";

    public OrderFlowTests(PostgresFixture postgres) : base(postgres) { }

    [Fact]
    public async Task A_client_orders_a_package_and_the_terms_are_frozen_onto_the_order()
    {
        var (factory, client, siteId) = await ArrangeAsync();

        var response = await client.PostAsync(OrdersPath, new
        {
            burialSiteId = siteId,
            packageCode = "basic",
            comment = "Ко дню памяти",
        });

        Assert.Equal(HttpStatusCode.Created, response.Status);
        Assert.Equal(OrderStatuses.Draft, response.Data?["status"]?.GetValue<string>());
        Assert.Equal("Базовый уход", response.Data?["packageTitle"]?.GetValue<string>());
        Assert.Equal(4900m, response.Data?["packagePriceFromRub"]?.GetValue<decimal>());

        // The number is what gets read aloud in a support call, so it is generated, not the id.
        Assert.StartsWith("ПР-", response.Data?["number"]?.GetValue<string>());

        // Changing the catalogue afterwards must not touch what was sold.
        await factory.WithDbAsync(async db =>
        {
            var pkg = await db.ServicePackages.FirstAsync(p => p.Code == "basic");
            pkg.PriceFromRub = 9_900m;
            pkg.Title = "Базовый уход (новая цена)";
            await db.SaveChangesAsync();
        });

        var orderId = response.Data!["id"]!.GetValue<long>();
        var reread = await client.GetAsync($"{OrdersPath}/{orderId}");

        Assert.Equal(4900m, reread.Data?["packagePriceFromRub"]?.GetValue<decimal>());
        Assert.Equal("Базовый уход", reread.Data?["packageTitle"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_thin_description_sends_the_order_to_location_review_instead_of_pricing()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCatalogAndCemeteryAsync(factory);
        var client = factory.CreateApiClient();
        await LoginAsync(factory, client, ClientEmail);

        // No plot, no coordinate, a landmark too thin to find anything by.
        var site = await client.PostAsync("/api/v1/burial-sites", new
        {
            cemeteryId,
            deceasedFullName = "Петров Пётр Петрович",
            landmarks = "где-то справа",
        });
        Assert.Equal(LocationQualities.Insufficient, site.Data?["locationQuality"]?.GetValue<string>());

        var order = await client.PostAsync(OrdersPath, new
        {
            burialSiteId = site.Data!["id"]!.GetValue<long>(),
            packageCode = "basic",
        });

        var submitted = await client.PostAsync($"{OrdersPath}/{order.Data!["id"]!.GetValue<long>()}/submit");

        // The service sells an inspection rather than dispatching someone to search blind.
        Assert.Equal(OrderStatuses.LocationReview, submitted.Data?["status"]?.GetValue<string>());
        Assert.Equal("Уточняем место", submitted.Data?["statusLabel"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_stranger_cannot_see_another_clients_order()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var order = await client.PostAsync(OrdersPath, new { burialSiteId = siteId, packageCode = "basic" });
        var orderId = order.Data!["id"]!.GetValue<long>();

        var stranger = factory.CreateApiClient();
        await LoginAsync(factory, stranger, StrangerEmail);

        var response = await stranger.GetAsync($"{OrdersPath}/{orderId}");

        // 404, not 403: a 403 confirms the order exists.
        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Empty((await stranger.GetAsync(OrdersPath)).Data!.AsArray());
    }

    [Fact]
    public async Task A_relative_with_view_only_access_cannot_spend_money()
    {
        var (factory, owner, siteId) = await ArrangeAsync();

        var invited = await owner.PostAsync($"/api/v1/burial-sites/{siteId}/members", new
        {
            contact = StrangerEmail,
            permission = BurialSitePermissions.View,
        });

        var relative = factory.CreateApiClient();
        await LoginAsync(factory, relative, StrangerEmail);
        await relative.PostAsync("/api/v1/burial-sites/invitations/accept", new
        {
            token = invited.Data!["token"]!.GetValue<string>(),
        });

        // They can see the record...
        Assert.Equal(HttpStatusCode.OK, (await relative.GetAsync($"/api/v1/burial-sites/{siteId}")).Status);

        // ...but ordering means spending money, and that right is granted explicitly.
        var order = await relative.PostAsync(OrdersPath, new { burialSiteId = siteId, packageCode = "basic" });
        Assert.Equal(HttpStatusCode.Forbidden, order.Status);
        Assert.Equal("forbidden", order.ErrorCode);
    }

    [Fact]
    public async Task The_client_accepts_a_specific_estimate_version_and_the_order_awaits_payment()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var orderId = await SubmitOrderAsync(client, siteId);

        var dispatcher = await DispatcherAsync(factory);
        await PublishEstimateAsync(dispatcher, orderId, version: 1, workPrice: 4_900m);

        var beforeAccept = await client.GetAsync($"{OrdersPath}/{orderId}");
        Assert.Equal(OrderStatuses.EstimateReady, beforeAccept.Data?["status"]?.GetValue<string>());
        Assert.Equal(4900m, beforeAccept.Data!["estimates"]![0]!["totalRub"]!.GetValue<decimal>());

        var accepted = await client.PostAsync($"{OrdersPath}/{orderId}/estimate-acceptance", new { version = 1 });

        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(OrderStatuses.AwaitingPayment, accepted.Data?["status"]?.GetValue<string>());
        Assert.Equal("Оплатить", accepted.Data?["cta"]?.GetValue<string>());
    }

    [Fact]
    public async Task Publishing_a_correction_invalidates_the_previous_agreement()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var orderId = await SubmitOrderAsync(client, siteId);
        var dispatcher = await DispatcherAsync(factory);

        await PublishEstimateAsync(dispatcher, orderId, version: 1, workPrice: 4_900m);
        await client.PostAsync($"{OrdersPath}/{orderId}/estimate-acceptance", new { version = 1 });

        // Something was found on site and the price changed. The client agreed to v1, so v1 is no
        // longer what is on offer — this is the whole point of BR-002.
        await PublishEstimateAsync(dispatcher, orderId, version: 2, workPrice: 6_400m);

        var order = await client.GetAsync($"{OrdersPath}/{orderId}");
        Assert.Equal(OrderStatuses.EstimateReady, order.Data?["status"]?.GetValue<string>());

        // Accepting the stale version — the case where the client had the page open — is refused.
        var stale = await client.PostAsync($"{OrdersPath}/{orderId}/estimate-acceptance", new { version = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, stale.Status);

        var fresh = await client.PostAsync($"{OrdersPath}/{orderId}/estimate-acceptance", new { version = 2 });
        Assert.Equal(HttpStatusCode.OK, fresh.Status);
        Assert.Equal(OrderStatuses.AwaitingPayment, fresh.Data?["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_client_never_sees_a_draft_estimate()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var orderId = await SubmitOrderAsync(client, siteId);
        var dispatcher = await DispatcherAsync(factory);

        // Created but not published: the dispatcher's workings.
        await dispatcher.PostAsync($"{AdminOrdersPath}/{orderId}/estimates", new
        {
            lines = new[] { new { type = "work", title = "Черновая прикидка", quantity = 1, unitPriceRub = 12_000m } },
        });

        var asClient = await client.GetAsync($"{OrdersPath}/{orderId}");
        Assert.Empty(asClient.Data!["estimates"]!.AsArray());
        Assert.Equal(OrderStatuses.Submitted, asClient.Data?["status"]?.GetValue<string>());

        // Staff see it, which is the point of having a draft at all.
        var asStaff = await dispatcher.GetAsync($"{AdminOrdersPath}/{orderId}");
        Assert.Single(asStaff.Data!["estimates"]!.AsArray());
    }

    [Fact]
    public async Task A_discount_bigger_than_the_work_is_refused()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var orderId = await SubmitOrderAsync(client, siteId);
        var dispatcher = await DispatcherAsync(factory);

        var response = await dispatcher.PostAsync($"{AdminOrdersPath}/{orderId}/estimates", new
        {
            lines = new object[]
            {
                new { type = "work", title = "Уборка", quantity = 1, unitPriceRub = 4_900m },
                new { type = "discount", title = "Скидка", quantity = 1, unitPriceRub = -9_000m },
            },
        });

        // BR-003: otherwise the operator owes the client money for doing the job.
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains("отрицательным", response.Errors![0]!["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_negative_price_is_only_allowed_on_a_discount_line()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var orderId = await SubmitOrderAsync(client, siteId);
        var dispatcher = await DispatcherAsync(factory);

        var response = await dispatcher.PostAsync($"{AdminOrdersPath}/{orderId}/estimates", new
        {
            lines = new[] { new { type = "work", title = "Уборка", quantity = 1, unitPriceRub = -500m } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public async Task An_empty_estimate_cannot_reach_the_client()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var orderId = await SubmitOrderAsync(client, siteId);
        var dispatcher = await DispatcherAsync(factory);

        var created = await dispatcher.PostAsync($"{AdminOrdersPath}/{orderId}/estimates", new
        {
            lines = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, created.Status);
    }

    [Fact]
    public async Task A_client_cannot_reach_the_dispatcher_queue()
    {
        var (factory, client, _) = await ArrangeAsync();

        var response = await client.GetAsync(AdminOrdersPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }

    [Fact]
    public async Task Every_status_change_is_recorded_with_its_reason()
    {
        var (factory, client, siteId) = await ArrangeAsync();
        var orderId = await SubmitOrderAsync(client, siteId);
        var dispatcher = await DispatcherAsync(factory);

        await PublishEstimateAsync(dispatcher, orderId, version: 1, workPrice: 4_900m);
        await client.PostAsync($"{OrdersPath}/{orderId}/estimate-acceptance", new { version = 1 });

        var history = await factory.QueryDbAsync(db => db.OrderStatusHistory
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.Id)
            .Select(h => h.ToStatus)
            .ToListAsync());

        // "When did this become paid, and who moved it" is a dispute question; reconstructing it
        // from application logs is not an answer anyone can rely on.
        Assert.Equal(
            new[] { OrderStatuses.Draft, OrderStatuses.Submitted, OrderStatuses.EstimateReady, OrderStatuses.AwaitingPayment },
            history);
    }

    // -------------------------------------------------------------------------------------------

    private async Task<(PamyatApiFactory Factory, ApiClient Client, long SiteId)> ArrangeAsync()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCatalogAndCemeteryAsync(factory);

        var client = factory.CreateApiClient();
        await LoginAsync(factory, client, ClientEmail);

        var site = await client.PostAsync("/api/v1/burial-sites", new
        {
            cemeteryId,
            deceasedFullName = "Иванов Иван Иванович",
            plotSection = "уч. 12, ряд 3",
            landmarks = "третий ряд от часовни, синяя ограда",
        });

        return (factory, client, site.Data!["id"]!.GetValue<long>());
    }

    private static async Task<long> SeedCatalogAndCemeteryAsync(PamyatApiFactory factory)
    {
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

        return cemeteryId;
    }

    private async Task<ApiClient> DispatcherAsync(PamyatApiFactory factory)
    {
        var dispatcher = factory.CreateApiClient();
        await LoginAsync(factory, dispatcher, DispatcherEmail);

        // OTP registration always creates a client; the role is what this test is about.
        await factory.WithDbAsync(async db =>
        {
            var identity = await db.AuthIdentities.Include(i => i.User)
                .FirstAsync(i => i.Email == DispatcherEmail);
            identity.User!.Role = UserRoles.Dispatcher;
            await db.SaveChangesAsync();
        });

        return dispatcher;
    }

    private static async Task<long> SubmitOrderAsync(ApiClient client, long siteId)
    {
        var order = await client.PostAsync(OrdersPath, new { burialSiteId = siteId, packageCode = "basic" });
        var orderId = order.Data!["id"]!.GetValue<long>();
        await client.PostAsync($"{OrdersPath}/{orderId}/submit");
        return orderId;
    }

    private static async Task PublishEstimateAsync(ApiClient dispatcher, long orderId, int version, decimal workPrice)
    {
        var created = await dispatcher.PostAsync($"{AdminOrdersPath}/{orderId}/estimates", new
        {
            note = "По результатам осмотра",
            lines = new[] { new { type = "work", title = "Уборка участка", quantity = 1, unitPriceRub = workPrice } },
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);

        var published = await dispatcher.PostAsync($"{AdminOrdersPath}/{orderId}/estimates/{version}/publish");
        Assert.Equal(HttpStatusCode.OK, published.Status);
    }
}
