using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.BurialSites;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.BurialSites;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// The cemetery directory. Readable without a session on purpose: the public catalogue has to
/// show which cemeteries are served before anyone signs up, and the fields exposed here are the
/// same ones that appear on those pages.
///
/// Note what is NOT projected: <c>Rules</c> and <c>Contacts</c> are operational data for
/// executors and dispatchers (site restrictions, administration phone numbers) and stay
/// staff-only. Managing the directory belongs to the backoffice module and is not here yet.
/// </summary>
[ApiController]
[Route("api/v1/cemeteries")]
public sealed class CemeteriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CemeteriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CemeteryDto>>>> List(CancellationToken ct)
    {
        var items = await _db.Cemeteries
            .AsNoTracking()
            .Where(c => c.Status == CemeteryStatuses.Active)
            // Region first: the directory is national, so a client scans for their city before
            // they scan for a cemetery name.
            .OrderBy(c => c.Region)
            .ThenBy(c => c.Name)
            .Select(c => new CemeteryDto
            {
                Id = c.Id,
                Name = c.Name,
                Region = c.Region,
                Address = c.Address,
                Hours = c.Hours,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<CemeteryDto>>.Ok(items));
    }
}
