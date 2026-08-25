using System.Net;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.BurialSites;

/// <summary>
/// Input rules and the location-quality assessment that decides whether the service may sell a
/// package at all, or has to sell an inspection first.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BurialSiteValidationTests : AuthIntegrationTest
{
    private const string OwnerEmail = "validation-owner@example.com";
    private const string SitesPath = "/api/v1/burial-sites";

    public BurialSiteValidationTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task A_vague_description_is_marked_insufficient_so_an_inspection_is_sold_instead()
    {
        var (factory, client, cemeteryId) = await ArrangeAsync();

        var response = await client.PostAsync(SitesPath, new
        {
            cemeteryId,
            deceasedFullName = "Петров Пётр Петрович",
            // No plot, no coordinate, and a landmark too thin to find anything by.
            landmarks = "где-то справа",
        });

        Assert.Equal(HttpStatusCode.Created, response.Status);
        Assert.Equal(LocationQualities.Insufficient, response.Data?["locationQuality"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_plot_reference_backed_by_landmarks_is_enough_to_dispatch()
    {
        var (factory, client, cemeteryId) = await ArrangeAsync();

        var response = await client.PostAsync(SitesPath, new
        {
            cemeteryId,
            deceasedFullName = "Петров Пётр Петрович",
            plotSection = "уч. 12, ряд 3",
            landmarks = "третий ряд от часовни, синяя ограда",
        });

        Assert.Equal(LocationQualities.Sufficient, response.Data?["locationQuality"]?.GetValue<string>());
    }

    [Fact]
    public async Task An_exact_coordinate_alone_is_enough()
    {
        var (factory, client, cemeteryId) = await ArrangeAsync();

        var response = await client.PostAsync(SitesPath, new
        {
            cemeteryId,
            deceasedFullName = "Петров Пётр Петрович",
            geoLat = 60.0821234m,
            geoLng = 30.3141234m,
        });

        Assert.Equal(LocationQualities.Sufficient, response.Data?["locationQuality"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_staff_verdict_is_not_overwritten_by_the_heuristic_on_edit()
    {
        var (factory, client, cemeteryId) = await ArrangeAsync();

        var created = await client.PostAsync(SitesPath, new
        {
            cemeteryId,
            deceasedFullName = "Петров Пётр Петрович",
            plotSection = "уч. 12, ряд 3",
            landmarks = "третий ряд от часовни, синяя ограда",
        });
        var siteId = created.Data!["id"]!.GetValue<long>();

        // A dispatcher inspected the place and ruled it unfindable — evidence from the real world.
        await factory.WithDbAsync(async db =>
        {
            var site = await db.BurialSites.FindAsync(siteId);
            site!.LocationQuality = LocationQualities.Insufficient;
            await db.SaveChangesAsync();
        });

        var updated = await client.SendAsync(
            HttpMethod.Patch, $"{SitesPath}/{siteId}", new { landmarks = "очень подробное описание рядом с часовней" });

        // The heuristic would have said "sufficient"; the human verdict wins.
        Assert.Equal(LocationQualities.Insufficient, updated.Data?["locationQuality"]?.GetValue<string>());
    }

    [Fact]
    public async Task Half_a_coordinate_is_rejected()
    {
        var (factory, client, cemeteryId) = await ArrangeAsync();

        var response = await client.PostAsync(SitesPath, new
        {
            cemeteryId,
            deceasedFullName = "Петров Пётр Петрович",
            geoLat = 60.0821234m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("validation_error", response.ErrorCode);
    }

    [Fact]
    public async Task An_unknown_cemetery_is_rejected()
    {
        var (factory, client, _) = await ArrangeAsync();

        var response = await client.PostAsync(SitesPath, new
        {
            cemeteryId = 999_999L,
            deceasedFullName = "Петров Пётр Петрович",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("validation_error", response.ErrorCode);
    }

    [Fact]
    public async Task Incomplete_life_dates_are_accepted_as_written()
    {
        var (factory, client, cemeteryId) = await ArrangeAsync();

        // Relatives routinely know only a year, or a date that disagrees with the headstone.
        var response = await client.PostAsync(SitesPath, new
        {
            cemeteryId,
            deceasedFullName = "Петров Пётр Петрович",
            birthDateText = "около 1943",
            deathDateText = "март 1998 (по документам 1997)",
            plotSection = "уч. 12",
            landmarks = "третий ряд от часовни, синяя ограда",
        });

        Assert.Equal(HttpStatusCode.Created, response.Status);
        Assert.Equal("около 1943", response.Data?["birthDateText"]?.GetValue<string>());
        Assert.Equal("март 1998 (по документам 1997)", response.Data?["deathDateText"]?.GetValue<string>());
    }

    [Fact]
    public async Task The_cemetery_directory_is_readable_without_signing_in()
    {
        var (factory, _, _) = await ArrangeAsync();
        var anonymous = factory.CreateApiClient();

        var response = await anonymous.GetAsync("/api/v1/cemeteries");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Single(response.Data!.AsArray());

        // Operational fields stay staff-only: site rules and administration contacts are not
        // part of the public projection.
        var first = response.Data![0]!.AsObject();
        Assert.False(first.ContainsKey("rules"));
        Assert.False(first.ContainsKey("contacts"));
    }

    private async Task<(PamyatApiFactory Factory, ApiClient Client, long CemeteryId)> ArrangeAsync()
    {
        var factory = CreateFactory();

        long cemeteryId = 0;
        await factory.WithDbAsync(async db =>
        {
            var cemetery = new Cemetery { Name = "Южное кладбище", Region = "Екатеринбург" };
            db.Cemeteries.Add(cemetery);
            await db.SaveChangesAsync();
            cemeteryId = cemetery.Id;
        });

        var client = factory.CreateApiClient();
        await LoginAsync(factory, client, OwnerEmail);

        return (factory, client, cemeteryId);
    }
}
