using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>
/// The consent gate on account creation.
///
/// The rule these protect is legal, not cosmetic: under 152-ФЗ an account created without a
/// recorded consent has no lawful ground for the processing that follows. So the check has to
/// hold against a direct request, not only against the sign-in form.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationConsentTests : AuthIntegrationTest
{
    private const string Email = "consent@example.com";

    public RegistrationConsentTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Registration_is_refused_without_consent()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code, acceptedLegal: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("legal_not_accepted", response.ErrorCode);
        Assert.Null(response.SetSessionToken);
    }

    [Fact]
    public async Task A_refused_registration_creates_no_account_and_no_session()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);
        await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code, acceptedLegal: false));

        Assert.Equal(0, await factory.QueryDbAsync(db => db.Users.CountAsync()));
        Assert.Equal(0, await factory.QueryDbAsync(db => db.AuthIdentities.CountAsync()));
        Assert.Equal(0, await factory.QueryDbAsync(db => db.AuthSessions.CountAsync()));
        Assert.Equal(0, await factory.QueryDbAsync(db => db.ConsentLogs.CountAsync()));
    }

    [Fact]
    public async Task A_refusal_is_audited()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var code = await RequestCodeAsync(factory, client, Email);
        await client.PostAsync(OtpVerifyPath, OtpVerify(Email, code, acceptedLegal: false));

        var metadata = await LatestAuditMetadataAsync(factory, SecurityAuditEventTypes.LoginFailed);
        Assert.Equal("legal_not_accepted", metadata?["reason"]?.GetValue<string>());
    }

    [Fact]
    public async Task Consent_records_point_at_the_published_versions_not_a_placeholder()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();
        await LoginAsync(factory, client, Email);

        var consent = await factory.QueryDbAsync(db => db.ConsentLogs.SingleAsync());
        Assert.Equal(ConsentTypes.PersonalData, consent.ConsentType);
        Assert.NotEqual("v0-draft", consent.DocumentVersion);
        Assert.Equal("1.0", consent.DocumentVersion);

        // The policy and the consent are separate instruments, so both are recorded — a single
        // combined "agreed" would lose which of them the person was actually shown.
        var accepted = await factory.QueryDbAsync(db => db.LegalAcceptances
            .Join(db.LegalDocuments, a => a.DocumentId, d => d.Id, (a, d) => d.Type)
            .ToListAsync());

        Assert.Equal(2, accepted.Count);
        Assert.Contains(LegalDocumentTypes.Privacy, accepted);
        Assert.Contains(LegalDocumentTypes.OfertaClient, accepted);
    }

    [Fact]
    public async Task Published_documents_are_marked_published_and_dated()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();
        await LoginAsync(factory, client, Email);

        var documents = await factory.QueryDbAsync(db => db.LegalDocuments.ToListAsync());

        Assert.NotEmpty(documents);
        Assert.All(documents, d =>
        {
            Assert.Equal(LegalDocumentStatuses.Published, d.Status);
            Assert.NotNull(d.EffectiveAt);
            Assert.False(string.IsNullOrWhiteSpace(d.ContentHash));
        });
    }

    [Fact]
    public async Task A_returning_user_is_not_asked_to_consent_again()
    {
        var factory = CreateFactory();

        var first = factory.CreateApiClient();
        await LoginAsync(factory, first, Email);
        var consentsAfterRegistration = await factory.QueryDbAsync(db => db.ConsentLogs.CountAsync());

        // Signing in again without the flag: the account already exists, and the consent given at
        // registration is the record that stands.
        var second = factory.CreateApiClient();
        var code = await RequestCodeAsync(factory, second, Email);
        var response = await second.PostAsync(OtpVerifyPath, OtpVerify(Email, code, acceptedLegal: false));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull(response.SetSessionToken);
        Assert.Equal(consentsAfterRegistration, await factory.QueryDbAsync(db => db.ConsentLogs.CountAsync()));
    }

    [Fact]
    public async Task The_document_list_is_readable_without_signing_in()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateApiClient();

        var response = await anonymous.GetAsync("/api/v1/legal/documents");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var types = response.Data!.AsArray().Select(x => x!["type"]!.GetValue<string>()).ToList();
        Assert.Contains(LegalDocumentTypes.Privacy, types);
        Assert.Contains(LegalDocumentTypes.Cookies, types);
    }

    [Fact]
    public async Task Declining_optional_cookies_is_recorded_as_a_revocation_not_a_missing_row()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateApiClient();

        var declined = await anonymous.PostAsync("/api/v1/legal/cookie-consent", new { analytics = false });
        Assert.Equal(HttpStatusCode.OK, declined.Status);

        var row = await factory.QueryDbAsync(db => db.ConsentLogs
            .SingleAsync(x => x.ConsentType == ConsentTypes.Cookies));

        Assert.Null(row.UserId);                 // guest-safe: no account exists yet
        Assert.NotNull(row.RevokedAt);           // "asked and declined", not "never asked"
        Assert.Equal("1.0", row.DocumentVersion);
    }

    [Fact]
    public async Task Accepting_optional_cookies_is_recorded_as_live_consent()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateApiClient();

        await anonymous.PostAsync("/api/v1/legal/cookie-consent", new { analytics = true });

        var row = await factory.QueryDbAsync(db => db.ConsentLogs
            .SingleAsync(x => x.ConsentType == ConsentTypes.Cookies));

        Assert.Null(row.RevokedAt);
    }
}
