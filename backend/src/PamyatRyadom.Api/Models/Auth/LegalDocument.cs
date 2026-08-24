namespace PamyatRyadom.Api.Models.Auth;

/// <summary>One versioned legal document (an offer/terms, privacy policy, cookie policy, refund
/// policy). Real content is a separate task — for now AuthService creates a placeholder "v0-draft"
/// row per type on first use so registration has something to attach a <see cref="LegalAcceptance"/>
/// to. See AuthService.EnsurePlaceholderLegalDocumentAsync.</summary>
public sealed class LegalDocument
{
    public long Id { get; set; }
    public string Type { get; set; } = LegalDocumentTypes.Privacy;
    public string Version { get; set; } = string.Empty;
    public string Locale { get; set; } = "ru";
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; set; }
    public string Status { get; set; } = LegalDocumentStatuses.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<LegalAcceptance> Acceptances { get; set; } = new List<LegalAcceptance>();
}

public static class LegalDocumentTypes
{
    public const string OfertaClient = "oferta_client";
    public const string OfertaExecutor = "oferta_executor";
    public const string Privacy = "privacy";
    public const string Cookies = "cookies";
    public const string RefundPolicy = "refund_policy";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        OfertaClient, OfertaExecutor, Privacy, Cookies, RefundPolicy
    };
}

public static class LegalDocumentStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Archived = "archived";

    public static readonly IReadOnlyCollection<string> All = new[] { Draft, Published, Archived };
}
