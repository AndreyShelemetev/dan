using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Catalog;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Catalog;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Catalogue administration: what is sold, at what price, with what checklist.
///
/// Restricted to admin and superadmin. Prices and package composition are commercial terms of
/// the contract with every client, so this is not a surface where "dispatcher can probably do it
/// too" is a safe default — least privilege, per FR-ADM-014.
/// </summary>
[ApiController]
[Route("api/v1/admin/catalog")]
[RequireRole(UserRoles.Admin, UserRoles.Superadmin)]
public sealed class AdminCatalogController : AuthorizedControllerBase
{
    private readonly ICatalogAdminService _catalog;

    public AdminCatalogController(ICatalogAdminService catalog) => _catalog = catalog;

    // -- Packages ---------------------------------------------------------------------------------

    [HttpGet("packages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminPackageDto>>>> ListPackages(CancellationToken ct) =>
        Envelope(await _catalog.ListPackagesAsync(ct));

    [HttpPost("packages")]
    public async Task<ActionResult<ApiResponse<AdminPackageDto>>> CreatePackage(
        [FromBody] SavePackageDto dto, CancellationToken ct) =>
        Envelope(await _catalog.CreatePackageAsync(CurrentUserId, dto, ct));

    [HttpPatch("packages/{id:long}")]
    public async Task<ActionResult<ApiResponse<AdminPackageDto>>> UpdatePackage(
        long id, [FromBody] SavePackageDto dto, CancellationToken ct) =>
        Envelope(await _catalog.UpdatePackageAsync(CurrentUserId, id, dto, ct));

    [HttpPost("packages/{id:long}/publish")]
    public async Task<ActionResult<ApiResponse<AdminPackageDto>>> PublishPackage(long id, CancellationToken ct) =>
        Envelope(await _catalog.PublishPackageAsync(CurrentUserId, id, ct));

    [HttpPost("packages/{id:long}/archive")]
    public async Task<ActionResult<ApiResponse<AdminPackageDto>>> ArchivePackage(long id, CancellationToken ct) =>
        Envelope(await _catalog.ArchivePackageAsync(CurrentUserId, id, ct));

    /// <summary>Copies a version into a new draft — the supported way to change something that is
    /// already on sale.</summary>
    [HttpPost("packages/{id:long}/versions")]
    public async Task<ActionResult<ApiResponse<AdminPackageDto>>> NewPackageVersion(
        long id, [FromBody] NewVersionDto dto, CancellationToken ct) =>
        Envelope(await _catalog.NewPackageVersionAsync(CurrentUserId, id, dto.Version, ct));

    /// <summary>Deletes a draft version. Published versions are archived, never deleted — an
    /// order that points at one has to keep pointing at something.</summary>
    [HttpDelete("packages/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePackage(long id, CancellationToken ct) =>
        Envelope(await _catalog.DeletePackageAsync(CurrentUserId, id, ct));

    // -- Subscription plans -----------------------------------------------------------------------

    [HttpGet("plans")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminPlanDto>>>> ListPlans(CancellationToken ct) =>
        Envelope(await _catalog.ListPlansAsync(ct));

    [HttpPost("plans")]
    public async Task<ActionResult<ApiResponse<AdminPlanDto>>> CreatePlan(
        [FromBody] SavePlanDto dto, CancellationToken ct) =>
        Envelope(await _catalog.CreatePlanAsync(CurrentUserId, dto, ct));

    [HttpPatch("plans/{id:long}")]
    public async Task<ActionResult<ApiResponse<AdminPlanDto>>> UpdatePlan(
        long id, [FromBody] SavePlanDto dto, CancellationToken ct) =>
        Envelope(await _catalog.UpdatePlanAsync(CurrentUserId, id, dto, ct));

    [HttpPost("plans/{id:long}/publish")]
    public async Task<ActionResult<ApiResponse<AdminPlanDto>>> PublishPlan(long id, CancellationToken ct) =>
        Envelope(await _catalog.PublishPlanAsync(CurrentUserId, id, ct));

    [HttpPost("plans/{id:long}/archive")]
    public async Task<ActionResult<ApiResponse<AdminPlanDto>>> ArchivePlan(long id, CancellationToken ct) =>
        Envelope(await _catalog.ArchivePlanAsync(CurrentUserId, id, ct));

    [HttpDelete("plans/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePlan(long id, CancellationToken ct) =>
        Envelope(await _catalog.DeletePlanAsync(CurrentUserId, id, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}
