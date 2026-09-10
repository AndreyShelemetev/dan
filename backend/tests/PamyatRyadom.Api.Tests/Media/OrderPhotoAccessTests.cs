using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Catalog;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Media;

/// <summary>
/// Who may look at the photos a client attaches to their own request.
///
/// A dispatcher has to see the grave to price the job without sending anyone to look. That is
/// the whole reason the client uploads it — but "staff can read" must not quietly become "staff
/// can edit": the photos are the evidence an estimate was based on, and they belong to the
/// client.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderPhotoAccessTests : AuthIntegrationTest
{
    private const string OwnerEmail = "photo-owner@example.com";
    private const string StrangerEmail = "photo-stranger@example.com";
    private const string StaffEmail = "photo-dispatcher@example.com";
    private const string FinanceEmail = "photo-finance@example.com";

    public OrderPhotoAccessTests(PostgresFixture postgres) : base(postgres) { }

    [Fact]
    public async Task A_dispatcher_can_read_the_photos_on_an_order_they_did_not_create()
    {
        var (factory, owner, orderId) = await ArrangeAsync();
        var dispatcher = await StaffAsync(factory, StaffEmail, UserRoles.Dispatcher);

        var mine = await owner.GetAsync(Path(orderId));
        var theirs = await dispatcher.GetAsync(Path(orderId));

        Assert.Equal(HttpStatusCode.OK, mine.Status);
        Assert.Equal(HttpStatusCode.OK, theirs.Status);
    }

    [Fact]
    public async Task A_dispatcher_cannot_attach_photos_to_someone_elses_order()
    {
        var (factory, _, orderId) = await ArrangeAsync();
        var dispatcher = await StaffAsync(factory, StaffEmail, UserRoles.Dispatcher);

        var response = await dispatcher.PostAsync("/api/v1/media/upload-sessions", new
        {
            ownerType = "order",
            ownerId = orderId,
            phase = "reference",
            contentType = "image/jpeg",
            sizeBytes = 1024,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Fact]
    public async Task Finance_has_no_business_looking_at_a_grave()
    {
        var (factory, _, orderId) = await ArrangeAsync();
        var finance = await StaffAsync(factory, FinanceEmail, UserRoles.Finance);

        var response = await finance.GetAsync(Path(orderId));

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Fact]
    public async Task Another_client_sees_nothing()
    {
        var (factory, _, orderId) = await ArrangeAsync();

        var stranger = factory.CreateApiClient();
        await LoginAsync(factory, stranger, StrangerEmail);

        var response = await stranger.GetAsync(Path(orderId));

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    private static string Path(long orderId) => $"/api/v1/media?ownerType=order&ownerId={orderId}";

    private async Task<(PamyatApiFactory Factory, ApiClient Owner, long OrderId)> ArrangeAsync()
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

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);

        var site = await owner.PostAsync("/api/v1/burial-sites", new
        {
            cemeteryId,
            deceasedFullName = "Иванов Иван Иванович",
            plotSection = "уч. 12, ряд 3",
            landmarks = "третий ряд от часовни, синяя ограда",
        });

        var order = await owner.PostAsync("/api/v1/orders", new
        {
            burialSiteId = site.Data!["id"]!.GetValue<long>(),
            packageCode = "basic",
        });

        return (factory, owner, order.Data!["id"]!.GetValue<long>());
    }

    private async Task<ApiClient> StaffAsync(PamyatApiFactory factory, string email, string role)
    {
        var client = factory.CreateApiClient();
        await LoginAsync(factory, client, email);

        // OTP registration always creates a client; the role is what these tests are about.
        await factory.WithDbAsync(async db =>
        {
            var identity = await db.AuthIdentities.Include(i => i.User).FirstAsync(i => i.Email == email);
            identity.User!.Role = role;
            await db.SaveChangesAsync();
        });

        return client;
    }
}
