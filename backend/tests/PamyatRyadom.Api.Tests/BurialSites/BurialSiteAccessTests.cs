using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.BurialSites;

/// <summary>
/// Who can see and change a burial site.
///
/// This is the module's highest-risk surface: a record names a dead person, their plot and often
/// an exact coordinate, so a leak here is worse than a leak of most other data in the product.
/// The tests therefore lean on the negative cases.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BurialSiteAccessTests : AuthIntegrationTest
{
    private const string OwnerEmail = "owner@example.com";
    private const string StrangerEmail = "stranger@example.com";
    private const string RelativeEmail = "relative@example.com";

    private const string SitesPath = "/api/v1/burial-sites";

    public BurialSiteAccessTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Create_stores_the_record_and_returns_it_to_its_owner()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);
        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);

        var response = await owner.PostAsync(SitesPath, NewSite(cemeteryId));

        Assert.Equal(HttpStatusCode.Created, response.Status);
        Assert.Equal("Иванов Иван Иванович", response.Data?["deceasedFullName"]?.GetValue<string>());
        Assert.Equal(BurialSitePermissions.Manage, response.Data?["permission"]?.GetValue<string>());
        Assert.True(response.Data?["isOwner"]?.GetValue<bool>());
    }

    [Fact]
    public async Task A_stranger_gets_404_rather_than_403_for_someone_elses_record()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);
        var created = await owner.PostAsync(SitesPath, NewSite(cemeteryId));
        var siteId = created.Data!["id"]!.GetValue<long>();

        var stranger = factory.CreateApiClient();
        await LoginAsync(factory, stranger, StrangerEmail);

        var response = await stranger.GetAsync($"{SitesPath}/{siteId}");

        // 404, not 403 — a 403 would confirm that a record with this id exists, which already
        // tells a stranger that a particular person is buried somewhere we serve.
        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal("not_found", response.ErrorCode);
    }

    [Fact]
    public async Task List_shows_only_records_the_caller_may_see()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);
        await owner.PostAsync(SitesPath, NewSite(cemeteryId));

        var stranger = factory.CreateApiClient();
        await LoginAsync(factory, stranger, StrangerEmail);

        var ownerList = await owner.GetAsync(SitesPath);
        var strangerList = await stranger.GetAsync(SitesPath);

        Assert.Single(ownerList.Data!.AsArray());
        Assert.Empty(strangerList.Data!.AsArray());
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateApiClient();

        var response = await anonymous.GetAsync(SitesPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
    }

    [Fact]
    public async Task An_accepted_invitation_grants_read_access_but_not_editing()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);
        var created = await owner.PostAsync(SitesPath, NewSite(cemeteryId));
        var siteId = created.Data!["id"]!.GetValue<long>();

        var invited = await owner.PostAsync(
            $"{SitesPath}/{siteId}/members",
            new { contact = RelativeEmail, permission = BurialSitePermissions.View });

        Assert.Equal(HttpStatusCode.Created, invited.Status);
        var token = invited.Data!["token"]!.GetValue<string>();

        var relative = factory.CreateApiClient();
        await LoginAsync(factory, relative, RelativeEmail);

        // Before accepting, the record is invisible.
        Assert.Equal(HttpStatusCode.NotFound, (await relative.GetAsync($"{SitesPath}/{siteId}")).Status);

        var accepted = await relative.PostAsync($"{SitesPath}/invitations/accept", new { token });
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(BurialSitePermissions.View, accepted.Data?["permission"]?.GetValue<string>());
        Assert.False(accepted.Data?["isOwner"]?.GetValue<bool>());

        // Now readable...
        Assert.Equal(HttpStatusCode.OK, (await relative.GetAsync($"{SitesPath}/{siteId}")).Status);

        // ...but a viewer may not edit.
        var update = await relative.SendAsync(
            HttpMethod.Patch, $"{SitesPath}/{siteId}", new { landmarks = "изменено посторонним" });

        Assert.Equal(HttpStatusCode.Forbidden, update.Status);
        Assert.Equal("forbidden", update.ErrorCode);
    }

    [Fact]
    public async Task An_invitation_token_cannot_be_used_twice()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);
        var created = await owner.PostAsync(SitesPath, NewSite(cemeteryId));
        var siteId = created.Data!["id"]!.GetValue<long>();

        var invited = await owner.PostAsync(
            $"{SitesPath}/{siteId}/members",
            new { contact = RelativeEmail, permission = BurialSitePermissions.View });
        var token = invited.Data!["token"]!.GetValue<string>();

        var relative = factory.CreateApiClient();
        await LoginAsync(factory, relative, RelativeEmail);
        Assert.Equal(HttpStatusCode.OK, (await relative.PostAsync($"{SitesPath}/invitations/accept", new { token })).Status);

        // A third party who somehow obtained the same link gets nothing.
        var stranger = factory.CreateApiClient();
        await LoginAsync(factory, stranger, StrangerEmail);
        var replay = await stranger.PostAsync($"{SitesPath}/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.BadRequest, replay.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{SitesPath}/{siteId}")).Status);
    }

    [Fact]
    public async Task Revoking_a_member_takes_their_access_away()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);
        var created = await owner.PostAsync(SitesPath, NewSite(cemeteryId));
        var siteId = created.Data!["id"]!.GetValue<long>();

        var invited = await owner.PostAsync(
            $"{SitesPath}/{siteId}/members",
            new { contact = RelativeEmail, permission = BurialSitePermissions.View });
        var memberId = invited.Data!["member"]!["id"]!.GetValue<long>();
        var token = invited.Data!["token"]!.GetValue<string>();

        var relative = factory.CreateApiClient();
        await LoginAsync(factory, relative, RelativeEmail);
        await relative.PostAsync($"{SitesPath}/invitations/accept", new { token });
        Assert.Equal(HttpStatusCode.OK, (await relative.GetAsync($"{SitesPath}/{siteId}")).Status);

        var revoked = await owner.SendAsync(HttpMethod.Delete, $"{SitesPath}/{siteId}/members/{memberId}", body: null);
        Assert.Equal(HttpStatusCode.OK, revoked.Status);

        Assert.Equal(HttpStatusCode.NotFound, (await relative.GetAsync($"{SitesPath}/{siteId}")).Status);

        // The row survives revocation: who had access, and until when, is what a later dispute asks.
        var stillThere = await factory.QueryDbAsync(db =>
            db.BurialSiteMembers.CountAsync(m => m.Id == memberId && m.RevokedAt != null));
        Assert.Equal(1, stillThere);
    }

    [Fact]
    public async Task Only_the_owner_may_delete_and_the_delete_is_soft()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);
        var created = await owner.PostAsync(SitesPath, NewSite(cemeteryId));
        var siteId = created.Data!["id"]!.GetValue<long>();

        // Grant a relative full "manage" — they may edit, but deleting the family's record is
        // deliberately reserved to the owner.
        var invited = await owner.PostAsync(
            $"{SitesPath}/{siteId}/members",
            new { contact = RelativeEmail, permission = BurialSitePermissions.Manage });
        var token = invited.Data!["token"]!.GetValue<string>();

        var relative = factory.CreateApiClient();
        await LoginAsync(factory, relative, RelativeEmail);
        await relative.PostAsync($"{SitesPath}/invitations/accept", new { token });

        var managerEdit = await relative.SendAsync(
            HttpMethod.Patch, $"{SitesPath}/{siteId}", new { landmarks = "третий ряд от часовни, синяя ограда" });
        Assert.Equal(HttpStatusCode.OK, managerEdit.Status);

        var managerDelete = await relative.SendAsync(HttpMethod.Delete, $"{SitesPath}/{siteId}", body: null);
        Assert.Equal(HttpStatusCode.Forbidden, managerDelete.Status);

        var ownerDelete = await owner.SendAsync(HttpMethod.Delete, $"{SitesPath}/{siteId}", body: null);
        Assert.Equal(HttpStatusCode.OK, ownerDelete.Status);

        // Soft delete: the row stays so order history and evidence photos are not orphaned.
        var status = await factory.QueryDbAsync(db =>
            db.BurialSites.Where(s => s.Id == siteId).Select(s => s.Status).SingleAsync());
        Assert.Equal(BurialSiteStatuses.Deleted, status);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"{SitesPath}/{siteId}")).Status);
    }

    [Fact]
    public async Task A_viewer_does_not_see_other_relatives_full_contacts()
    {
        var factory = CreateFactory();
        var cemeteryId = await SeedCemeteryAsync(factory);

        var owner = factory.CreateApiClient();
        await LoginAsync(factory, owner, OwnerEmail);
        var created = await owner.PostAsync(SitesPath, NewSite(cemeteryId));
        var siteId = created.Data!["id"]!.GetValue<long>();

        var invited = await owner.PostAsync(
            $"{SitesPath}/{siteId}/members",
            new { contact = RelativeEmail, permission = BurialSitePermissions.View });
        var token = invited.Data!["token"]!.GetValue<string>();

        var relative = factory.CreateApiClient();
        await LoginAsync(factory, relative, RelativeEmail);
        await relative.PostAsync($"{SitesPath}/invitations/accept", new { token });

        var asOwner = await owner.GetAsync($"{SitesPath}/{siteId}/members");
        var asViewer = await relative.GetAsync($"{SitesPath}/{siteId}/members");

        Assert.Equal(RelativeEmail, asOwner.Data![0]!["contact"]!.GetValue<string>());

        var maskedContact = asViewer.Data![0]!["contact"]!.GetValue<string>();
        Assert.NotEqual(RelativeEmail, maskedContact);
        Assert.Contains("•", maskedContact);
    }

    // -------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------

    private static object NewSite(long cemeteryId, string? plotSection = "уч. 12, ряд 3", string? landmarks = "третий ряд от часовни, синяя ограда") =>
        new
        {
            cemeteryId,
            deceasedFullName = "Иванов Иван Иванович",
            birthDateText = "1934",
            deathDateText = "12 марта 1998",
            plotSection,
            landmarks,
        };

    private static async Task<long> SeedCemeteryAsync(PamyatApiFactory factory)
    {
        long id = 0;
        await factory.WithDbAsync(async db =>
        {
            var cemetery = new Cemetery
            {
                Name = "Северное кладбище",
                Region = "Москва",
                Address = "Хованское шоссе",
            };

            db.Cemeteries.Add(cemetery);
            await db.SaveChangesAsync();
            id = cemetery.Id;
        });

        return id;
    }
}
