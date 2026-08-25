using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.BurialSites;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Media;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Services.BurialSites;

public interface IBurialSiteService
{
    Task<ServiceResult<IReadOnlyList<BurialSiteDto>>> ListAsync(long userId, CancellationToken ct = default);
    Task<ServiceResult<BurialSiteDto>> GetAsync(long userId, long id, CancellationToken ct = default);
    Task<ServiceResult<BurialSiteDto>> CreateAsync(long userId, CreateBurialSiteDto dto, CancellationToken ct = default);
    Task<ServiceResult<BurialSiteDto>> UpdateAsync(long userId, long id, UpdateBurialSiteDto dto, CancellationToken ct = default);
    Task<ServiceResult<object>> DeleteAsync(long userId, long id, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<BurialSiteMemberDto>>> ListMembersAsync(long userId, long siteId, CancellationToken ct = default);
    Task<ServiceResult<InvitationCreatedResult>> InviteMemberAsync(long userId, long siteId, InviteMemberDto dto, CancellationToken ct = default);
    Task<ServiceResult<object>> RevokeMemberAsync(long userId, long siteId, long memberId, CancellationToken ct = default);
    Task<ServiceResult<BurialSiteDto>> AcceptInvitationAsync(long userId, string token, CancellationToken ct = default);

    /// <summary>Resolves what <paramref name="userId"/> may do with a site, or null if the site
    /// is invisible to them. Other modules (orders, media) call this rather than re-deriving
    /// access rules, so there is exactly one place where this can be got wrong.</summary>
    Task<string?> ResolvePermissionAsync(long userId, long siteId, CancellationToken ct = default);
}

/// <summary>The invitation token is returned exactly once, at creation. Only its hash is
/// stored, so it can never be shown again — the same rule the session and OTP code follow.</summary>
public sealed record InvitationCreatedResult(BurialSiteMemberDto Member, string Token);

public sealed class BurialSiteService : IBurialSiteService
{
    private static readonly TimeSpan InvitationTtl = TimeSpan.FromDays(14);

    private readonly AppDbContext _db;
    private readonly ILogger<BurialSiteService> _logger;

    public BurialSiteService(AppDbContext db, ILogger<BurialSiteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string?> ResolvePermissionAsync(long userId, long siteId, CancellationToken ct = default)
    {
        var site = await _db.BurialSites
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == siteId && s.Status == BurialSiteStatuses.Active, ct);

        if (site is null)
        {
            return null;
        }

        if (site.OwnerUserId == userId)
        {
            return BurialSitePermissions.Manage;
        }

        var member = await _db.BurialSiteMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.BurialSiteId == siteId && m.UserId == userId && m.AcceptedAt != null && m.RevokedAt == null,
                ct);

        return member?.Permission;
    }

    public async Task<ServiceResult<IReadOnlyList<BurialSiteDto>>> ListAsync(long userId, CancellationToken ct = default)
    {
        // Owned sites, plus sites shared with this user through an accepted, live membership.
        var memberSiteIds = _db.BurialSiteMembers
            .Where(m => m.UserId == userId && m.AcceptedAt != null && m.RevokedAt == null)
            .Select(m => m.BurialSiteId);

        var sites = await _db.BurialSites
            .AsNoTracking()
            .Include(s => s.Cemetery)
            .Where(s => s.Status == BurialSiteStatuses.Active
                        && (s.OwnerUserId == userId || memberSiteIds.Contains(s.Id)))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var result = new List<BurialSiteDto>(sites.Count);
        foreach (var site in sites)
        {
            var permission = site.OwnerUserId == userId
                ? BurialSitePermissions.Manage
                : await ResolvePermissionAsync(userId, site.Id, ct) ?? BurialSitePermissions.View;

            result.Add(await MapAsync(site, permission, userId, ct));
        }

        return ServiceResult<IReadOnlyList<BurialSiteDto>>.Ok(result);
    }

    public async Task<ServiceResult<BurialSiteDto>> GetAsync(long userId, long id, CancellationToken ct = default)
    {
        var permission = await ResolvePermissionAsync(userId, id, ct);
        if (permission is null)
        {
            // Deliberately 404, not 403: telling a stranger that a record exists but is not
            // theirs already leaks that a given person is buried somewhere we serve.
            return ServiceResult<BurialSiteDto>.NotFound();
        }

        var site = await _db.BurialSites
            .AsNoTracking()
            .Include(s => s.Cemetery)
            .FirstAsync(s => s.Id == id, ct);

        return ServiceResult<BurialSiteDto>.Ok(await MapAsync(site, permission, userId, ct));
    }

    public async Task<ServiceResult<BurialSiteDto>> CreateAsync(long userId, CreateBurialSiteDto dto, CancellationToken ct = default)
    {
        var cemetery = await _db.Cemeteries
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CemeteryId && c.Status == CemeteryStatuses.Active, ct);

        if (cemetery is null)
        {
            return ServiceResult<BurialSiteDto>.Validation("Кладбище не найдено.", new { field = "cemeteryId" });
        }

        // Coordinates only make sense as a pair.
        if (dto.GeoLat is null != dto.GeoLng is null)
        {
            return ServiceResult<BurialSiteDto>.Validation("Координата задаётся парой широта + долгота.");
        }

        var site = new BurialSite
        {
            CemeteryId = dto.CemeteryId,
            OwnerUserId = userId,
            DeceasedFullName = dto.DeceasedFullName.Trim(),
            BirthDateText = Normalise(dto.BirthDateText),
            DeathDateText = Normalise(dto.DeathDateText),
            PlotSection = Normalise(dto.PlotSection),
            Landmarks = Normalise(dto.Landmarks),
            GeoLat = dto.GeoLat,
            GeoLng = dto.GeoLng,
            Notes = Normalise(dto.Notes),
            LocationQuality = AssessLocation(dto.PlotSection, dto.Landmarks, dto.GeoLat),
        };

        _db.BurialSites.Add(site);
        await _db.SaveChangesAsync(ct);

        // Identifiers only: the deceased's name, the plot and the coordinates are all PII or
        // close to it, and must never reach a free-form log.
        _logger.LogInformation("Burial site {SiteId} created by user {UserId}", site.Id, userId);

        site.Cemetery = await _db.Cemeteries.AsNoTracking().FirstAsync(c => c.Id == site.CemeteryId, ct);
        return ServiceResult<BurialSiteDto>.Created(await MapAsync(site, BurialSitePermissions.Manage, userId, ct));
    }

    public async Task<ServiceResult<BurialSiteDto>> UpdateAsync(long userId, long id, UpdateBurialSiteDto dto, CancellationToken ct = default)
    {
        var permission = await ResolvePermissionAsync(userId, id, ct);
        if (permission is null)
        {
            return ServiceResult<BurialSiteDto>.NotFound();
        }

        if (permission != BurialSitePermissions.Manage)
        {
            return ServiceResult<BurialSiteDto>.Forbidden("Изменять карточку может только владелец или участник с правом управления.");
        }

        var site = await _db.BurialSites.FirstAsync(s => s.Id == id, ct);

        if (dto.DeceasedFullName is not null) site.DeceasedFullName = dto.DeceasedFullName.Trim();
        if (dto.BirthDateText is not null) site.BirthDateText = Normalise(dto.BirthDateText);
        if (dto.DeathDateText is not null) site.DeathDateText = Normalise(dto.DeathDateText);
        if (dto.PlotSection is not null) site.PlotSection = Normalise(dto.PlotSection);
        if (dto.Landmarks is not null) site.Landmarks = Normalise(dto.Landmarks);
        if (dto.Notes is not null) site.Notes = Normalise(dto.Notes);

        if (dto.GeoLat is not null || dto.GeoLng is not null)
        {
            if (dto.GeoLat is null || dto.GeoLng is null)
            {
                return ServiceResult<BurialSiteDto>.Validation("Координата задаётся парой широта + долгота.");
            }

            site.GeoLat = dto.GeoLat;
            site.GeoLng = dto.GeoLng;
        }

        // Re-assess only while staff have not ruled on it: an operator's verdict is evidence
        // from a real inspection and must not be overwritten by a heuristic.
        if (site.LocationQuality == LocationQualities.Unverified)
        {
            site.LocationQuality = AssessLocation(site.PlotSection, site.Landmarks, site.GeoLat);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Burial site {SiteId} updated by user {UserId}", site.Id, userId);

        site.Cemetery = await _db.Cemeteries.AsNoTracking().FirstAsync(c => c.Id == site.CemeteryId, ct);
        return ServiceResult<BurialSiteDto>.Ok(await MapAsync(site, permission, userId, ct));
    }

    public async Task<ServiceResult<object>> DeleteAsync(long userId, long id, CancellationToken ct = default)
    {
        var site = await _db.BurialSites
            .FirstOrDefaultAsync(s => s.Id == id && s.Status == BurialSiteStatuses.Active, ct);

        if (site is null)
        {
            return ServiceResult<object>.NotFound();
        }

        // Only the owner may remove the record — a "manage" relative can edit it, but deleting
        // a family's shared memory record is not something to delegate implicitly.
        if (site.OwnerUserId != userId)
        {
            return ServiceResult<object>.Forbidden("Удалить карточку может только её владелец.");
        }

        // Soft delete via status, per the repo convention: order history and evidence photos
        // must survive, and a hard delete would orphan both.
        site.Status = BurialSiteStatuses.Deleted;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Burial site {SiteId} soft-deleted by user {UserId}", id, userId);
        return ServiceResult<object>.Ok(new { deleted = true });
    }

    public async Task<ServiceResult<IReadOnlyList<BurialSiteMemberDto>>> ListMembersAsync(long userId, long siteId, CancellationToken ct = default)
    {
        var permission = await ResolvePermissionAsync(userId, siteId, ct);
        if (permission is null)
        {
            return ServiceResult<IReadOnlyList<BurialSiteMemberDto>>.NotFound();
        }

        var isManager = permission == BurialSitePermissions.Manage;

        var members = await _db.BurialSiteMembers
            .AsNoTracking()
            .Where(m => m.BurialSiteId == siteId && m.RevokedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var dtos = members
            .Select(m => new BurialSiteMemberDto
            {
                Id = m.Id,
                UserId = m.UserId,
                // Only a manager sees who exactly was invited; for everyone else the list
                // confirms that others have access without handing out their addresses.
                Contact = isManager ? m.InvitedContact : MaskContact(m.InvitedContact),
                Permission = m.Permission,
                Accepted = m.AcceptedAt is not null,
                AcceptedAt = m.AcceptedAt,
                CreatedAt = m.CreatedAt,
            })
            .ToList();

        return ServiceResult<IReadOnlyList<BurialSiteMemberDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<InvitationCreatedResult>> InviteMemberAsync(long userId, long siteId, InviteMemberDto dto, CancellationToken ct = default)
    {
        var permission = await ResolvePermissionAsync(userId, siteId, ct);
        if (permission is null)
        {
            return ServiceResult<InvitationCreatedResult>.NotFound();
        }

        if (permission != BurialSitePermissions.Manage)
        {
            return ServiceResult<InvitationCreatedResult>.Forbidden("Приглашать участников может только владелец или участник с правом управления.");
        }

        if (!BurialSitePermissions.All.Contains(dto.Permission))
        {
            return ServiceResult<InvitationCreatedResult>.Validation("Недопустимый уровень доступа.", new { field = "permission" });
        }

        var contact = dto.Contact.Trim().ToLowerInvariant();

        var alreadyInvited = await _db.BurialSiteMembers
            .AnyAsync(m => m.BurialSiteId == siteId && m.InvitedContact == contact && m.RevokedAt == null, ct);

        if (alreadyInvited)
        {
            return ServiceResult<InvitationCreatedResult>.Validation("Этот человек уже приглашён.");
        }

        var token = SecretHasher.GenerateOpaqueToken();

        var member = new BurialSiteMember
        {
            BurialSiteId = siteId,
            InvitedContact = contact,
            Permission = dto.Permission,
            InvitedByUserId = userId,
            // High-entropy value: a fast hash is right here, the same reasoning as session tokens.
            InvitationTokenHash = SecretHasher.HashHighEntropy(token),
            InvitationExpiresAt = DateTimeOffset.UtcNow.Add(InvitationTtl),
        };

        _db.BurialSiteMembers.Add(member);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Invitation {MemberId} created for site {SiteId} by user {UserId}", member.Id, siteId, userId);

        var memberDto = new BurialSiteMemberDto
        {
            Id = member.Id,
            UserId = null,
            Contact = contact,
            Permission = member.Permission,
            Accepted = false,
            CreatedAt = member.CreatedAt,
        };

        return ServiceResult<InvitationCreatedResult>.Created(new InvitationCreatedResult(memberDto, token));
    }

    public async Task<ServiceResult<object>> RevokeMemberAsync(long userId, long siteId, long memberId, CancellationToken ct = default)
    {
        var permission = await ResolvePermissionAsync(userId, siteId, ct);
        if (permission is null)
        {
            return ServiceResult<object>.NotFound();
        }

        if (permission != BurialSitePermissions.Manage)
        {
            return ServiceResult<object>.Forbidden("Отзывать доступ может только владелец или участник с правом управления.");
        }

        var member = await _db.BurialSiteMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.BurialSiteId == siteId && m.RevokedAt == null, ct);

        if (member is null)
        {
            return ServiceResult<object>.NotFound("Участник не найден.");
        }

        // Kept as a revoked row rather than deleted: who had access to a family's records, and
        // until when, is exactly the kind of question a dispute asks later.
        member.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Member {MemberId} revoked from site {SiteId} by user {UserId}", memberId, siteId, userId);
        return ServiceResult<object>.Ok(new { revoked = true });
    }

    public async Task<ServiceResult<BurialSiteDto>> AcceptInvitationAsync(long userId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ServiceResult<BurialSiteDto>.Validation("Приглашение не найдено.");
        }

        var hash = SecretHasher.HashHighEntropy(token);

        var member = await _db.BurialSiteMembers
            .FirstOrDefaultAsync(m => m.InvitationTokenHash == hash && m.RevokedAt == null && m.AcceptedAt == null, ct);

        if (member is null)
        {
            return ServiceResult<BurialSiteDto>.Validation("Приглашение недействительно или уже использовано.");
        }

        if (member.InvitationExpiresAt is not null && member.InvitationExpiresAt < DateTimeOffset.UtcNow)
        {
            return ServiceResult<BurialSiteDto>.Validation("Срок действия приглашения истёк.");
        }

        var site = await _db.BurialSites
            .Include(s => s.Cemetery)
            .FirstOrDefaultAsync(s => s.Id == member.BurialSiteId && s.Status == BurialSiteStatuses.Active, ct);

        if (site is null)
        {
            return ServiceResult<BurialSiteDto>.NotFound();
        }

        if (site.OwnerUserId == userId)
        {
            return ServiceResult<BurialSiteDto>.Validation("Вы и так владелец этой карточки.");
        }

        var existing = await _db.BurialSiteMembers
            .FirstOrDefaultAsync(m => m.BurialSiteId == site.Id && m.UserId == userId && m.RevokedAt == null, ct);

        if (existing is not null)
        {
            return ServiceResult<BurialSiteDto>.Validation("У вас уже есть доступ к этой карточке.");
        }

        member.UserId = userId;
        member.AcceptedAt = DateTimeOffset.UtcNow;
        // Burn the token: accepted once, never reusable.
        member.InvitationTokenHash = null;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Invitation {MemberId} accepted by user {UserId}", member.Id, userId);
        return ServiceResult<BurialSiteDto>.Ok(await MapAsync(site, member.Permission, userId, ct));
    }

    private async Task<BurialSiteDto> MapAsync(BurialSite site, string permission, long userId, CancellationToken ct)
    {
        var photoCount = await _db.MediaAssets
            .CountAsync(
                a => a.OwnerType == MediaOwnerTypes.BurialSite
                     && a.OwnerId == site.Id
                     && a.Status == MediaStatuses.Ready,
                ct);

        return new BurialSiteDto
        {
            Id = site.Id,
            CemeteryId = site.CemeteryId,
            CemeteryName = site.Cemetery?.Name,
            DeceasedFullName = site.DeceasedFullName,
            BirthDateText = site.BirthDateText,
            DeathDateText = site.DeathDateText,
            PlotSection = site.PlotSection,
            Landmarks = site.Landmarks,
            GeoLat = site.GeoLat,
            GeoLng = site.GeoLng,
            Notes = site.Notes,
            LocationQuality = site.LocationQuality,
            Permission = permission,
            IsOwner = site.OwnerUserId == userId,
            PhotoCount = photoCount,
            CreatedAt = site.CreatedAt,
        };
    }

    /// <summary>
    /// First-pass judgement on whether an executor could actually find this grave.
    ///
    /// Deliberately crude and deliberately pessimistic: it only ever produces "sufficient" or
    /// "insufficient" as a starting point for a dispatcher, who has the final say. Getting this
    /// wrong in the optimistic direction means sending someone to wander a cemetery, so an
    /// exact coordinate, or a plot reference backed by landmarks, is the bar.
    /// </summary>
    private static string AssessLocation(string? plotSection, string? landmarks, decimal? geoLat)
    {
        if (geoLat is not null)
        {
            return LocationQualities.Sufficient;
        }

        var hasPlot = !string.IsNullOrWhiteSpace(plotSection);
        var hasLandmarks = !string.IsNullOrWhiteSpace(landmarks) && landmarks!.Trim().Length >= 15;

        return hasPlot && hasLandmarks ? LocationQualities.Sufficient : LocationQualities.Insufficient;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? MaskContact(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact))
        {
            return null;
        }

        var at = contact.IndexOf('@');
        if (at <= 0)
        {
            return "•••";
        }

        var name = contact[..at];
        var visible = name.Length <= 2 ? name[..1] : name[..2];
        return $"{visible}•••{contact[at..]}";
    }
}
