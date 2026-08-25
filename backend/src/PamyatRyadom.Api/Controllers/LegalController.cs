using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Legal;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Published legal documents and the consent records people give against them.
///
/// Open to anonymous callers by design: a visitor has to be able to read the policy and answer
/// the cookie banner before there is any account to attach the answer to. `consent_logs` is
/// guest-safe for exactly this reason.
/// </summary>
[ApiController]
[Route("api/v1/legal")]
public sealed class LegalController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILegalDocumentRegistry _registry;
    private readonly ILogger<LegalController> _logger;

    public LegalController(AppDbContext db, ILegalDocumentRegistry registry, ILogger<LegalController> logger)
    {
        _db = db;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>The versions currently in force, so the frontend stamps the page and records an
    /// acceptance against the same version rather than a number typed into the markup.</summary>
    [HttpGet("documents")]
    public ActionResult<ApiResponse<IReadOnlyList<LegalDocumentDto>>> Documents()
    {
        var items = _registry.Published
            .Select(d => new LegalDocumentDto
            {
                Type = d.Type,
                Version = d.Version,
                Title = d.Title,
                Path = d.Path,
                EffectiveAt = d.EffectiveAt,
                RequiredForRegistration = _registry.RequiredForRegistration.Any(r => r.Type == d.Type),
            })
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<LegalDocumentDto>>.Ok(items));
    }

    /// <summary>
    /// Records the visitor's answer to the cookie banner.
    ///
    /// Written to the database rather than left in browser storage: under 152-ФЗ a consent is
    /// only worth anything if the operator can show it was given, and a value the user can clear
    /// from their own devtools proves nothing. Rejecting the optional categories is recorded too
    /// — "asked and declined" is a materially different fact from "never asked".
    /// </summary>
    [HttpPost("cookie-consent")]
    public async Task<ActionResult<ApiResponse<object>>> CookieConsent(
        [FromBody] CookieConsentDto dto, CancellationToken ct)
    {
        var definition = _registry.Find(LegalDocumentTypes.Cookies);
        var now = DateTimeOffset.UtcNow;

        _db.ConsentLogs.Add(new ConsentLog
        {
            // Null when the visitor is not signed in, which is the normal case for a banner shown
            // on first visit.
            UserId = null,
            ConsentType = ConsentTypes.Cookies,
            DocumentVersion = definition?.Version ?? "unknown",
            IpAddress = HttpContext.Connection.RemoteIpAddress ?? System.Net.IPAddress.None,
            UserAgent = Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
                ? ua[..Math.Min(ua.Length, 512)]
                : null,
            AcceptedAt = now,
            // Declining the optional categories is a revocation of everything beyond the
            // strictly necessary ones, and is stored as such rather than as a missing row.
            RevokedAt = dto.Analytics ? null : now,
        });

        await _db.SaveChangesAsync(ct);

        // Categories only — never the visitor's address or agent string.
        _logger.LogInformation("Cookie consent recorded, analytics={Analytics}", dto.Analytics);

        return Ok(ApiResponse<object>.Ok(new { recorded = true, version = definition?.Version }));
    }
}

public sealed class LegalDocumentDto
{
    public string Type { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; init; }
    public bool RequiredForRegistration { get; init; }
}

public sealed class CookieConsentDto
{
    /// <summary>Strictly necessary cookies are not offered as a choice — the session cookie is
    /// what makes signing in work at all — so only the optional categories appear here.</summary>
    [Required]
    public bool Analytics { get; init; }
}
