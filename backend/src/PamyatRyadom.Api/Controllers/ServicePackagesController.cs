using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Catalog;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Catalog;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// The public catalogue.
///
/// Anonymous by design: the whole point of the catalogue is that someone can read what a package
/// includes, what it costs and — most importantly — what it does *not* cover, before deciding
/// whether to create an account.
///
/// Only published versions are exposed. Drafts are work in progress and archived versions exist
/// solely so orders sold under them keep their terms.
/// </summary>
[ApiController]
[Route("api/v1/service-packages")]
public sealed class ServicePackagesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ServicePackagesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServicePackageDto>>>> List(CancellationToken ct)
    {
        var packages = await _db.ServicePackages
            .AsNoTracking()
            .Where(p => p.Status == ServicePackageStatuses.Published && p.Locale == "ru")
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.PriceFromRub)
            .ToListAsync(ct);

        // Newest published version per code: a code may have several published versions over
        // time, and the catalogue sells the current one while old orders keep theirs.
        var current = packages
            .GroupBy(p => p.Code)
            .Select(g => g.OrderByDescending(p => p.PublishedAt).First())
            .OrderBy(p => p.SortOrder)
            .Select(Map)
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<ServicePackageDto>>.Ok(current));
    }

    private static ServicePackageDto Map(ServicePackage p) => new()
    {
        Code = p.Code,
        Version = p.Version,
        Title = p.Title,
        Summary = p.Summary,
        Includes = ReadLines(p.Includes),
        Limits = ReadLines(p.Limits),
        PriceFromRub = p.PriceFromRub,
        WarrantyDays = p.WarrantyDays,
        VisitsLabel = p.VisitsLabel,
    };

    private static IReadOnlyList<string> ReadLines(JsonDocument? document)
    {
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return document.RootElement
            .EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
    }
}
