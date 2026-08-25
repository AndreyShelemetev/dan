using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Services.Legal;

/// <summary>
/// The published legal documents, and the one place their version numbers are declared.
///
/// The wording lives in the frontend pages under `app/legal/`; this owns only the identity of
/// each version — which text was in force, when, and what a given user accepted. That split is
/// what makes an acceptance meaningful months later: a row in `legal_acceptances` points at a
/// version here, and a published version is never edited (BR-016), only superseded.
///
/// **Editing a document means bumping its version here in the same change.** Text changed
/// without a new version silently rewrites what past users are recorded as having agreed to.
/// </summary>
public sealed record LegalDocumentDefinition(
    string Type,
    string Version,
    string Title,
    string Path,
    DateTimeOffset EffectiveAt);

public interface ILegalDocumentRegistry
{
    IReadOnlyList<LegalDocumentDefinition> Published { get; }

    LegalDocumentDefinition? Find(string type);

    /// <summary>Documents a person must accept before an account is created.</summary>
    IReadOnlyList<LegalDocumentDefinition> RequiredForRegistration { get; }

    /// <summary>Ensures every published definition exists as a row, and returns them by type.</summary>
    Task<IReadOnlyDictionary<string, LegalDocument>> EnsurePublishedAsync(AppDbContext db, CancellationToken ct = default);
}

public sealed class LegalDocumentRegistry : ILegalDocumentRegistry
{
    // 2026-08-25: first published set, written for this service's actual processing.
    private static readonly DateTimeOffset FirstPublication = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);

    public IReadOnlyList<LegalDocumentDefinition> Published { get; } = new[]
    {
        new LegalDocumentDefinition(
            LegalDocumentTypes.Privacy, "1.0",
            "Политика обработки персональных данных", "/legal/privacy/", FirstPublication),
        new LegalDocumentDefinition(
            LegalDocumentTypes.Cookies, "1.0",
            "Политика использования cookie", "/legal/cookies/", FirstPublication),
        new LegalDocumentDefinition(
            LegalDocumentTypes.OfertaClient, "1.0",
            "Согласие на обработку персональных данных", "/legal/consent/", FirstPublication),
    };

    /// <summary>
    /// Both the privacy policy and the consent are required: 152-ФЗ treats them as different
    /// instruments. The policy is what the operator publishes about its processing; the consent
    /// is the legal ground the person gives. Accepting one is not accepting the other.
    /// </summary>
    public IReadOnlyList<LegalDocumentDefinition> RequiredForRegistration =>
        Published.Where(d => d.Type is LegalDocumentTypes.Privacy or LegalDocumentTypes.OfertaClient).ToList();

    public LegalDocumentDefinition? Find(string type) =>
        Published.FirstOrDefault(d => d.Type == type);

    public async Task<IReadOnlyDictionary<string, LegalDocument>> EnsurePublishedAsync(
        AppDbContext db, CancellationToken ct = default)
    {
        var result = new Dictionary<string, LegalDocument>();

        foreach (var definition in Published)
        {
            var existing = await db.LegalDocuments
                .FirstOrDefaultAsync(
                    x => x.Type == definition.Type && x.Version == definition.Version && x.Locale == "ru",
                    ct);

            if (existing is not null)
            {
                result[definition.Type] = existing;
                continue;
            }

            var document = new LegalDocument
            {
                Type = definition.Type,
                Version = definition.Version,
                Locale = "ru",
                // Identity of the version, not a checksum of prose the backend never sees. Its
                // job is to make two different versions distinguishable in the audit trail.
                ContentHash = SecretHasher.HashHighEntropy($"{definition.Type}:{definition.Version}:ru"),
                EffectiveAt = definition.EffectiveAt,
                Status = LegalDocumentStatuses.Published,
            };

            db.LegalDocuments.Add(document);
            result[definition.Type] = document;
        }

        if (db.ChangeTracker.HasChanges())
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Another instance published the same versions first. Re-read rather than fail:
                // this runs on every startup and must be safe to lose the race.
                db.ChangeTracker.Clear();
                result.Clear();
                foreach (var definition in Published)
                {
                    var row = await db.LegalDocuments.FirstAsync(
                        x => x.Type == definition.Type && x.Version == definition.Version && x.Locale == "ru",
                        ct);
                    result[definition.Type] = row;
                }
            }
        }

        return result;
    }
}
