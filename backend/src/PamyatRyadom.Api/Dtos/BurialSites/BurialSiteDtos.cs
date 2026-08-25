using System.ComponentModel.DataAnnotations;

namespace PamyatRyadom.Api.Dtos.BurialSites;

/// <summary>What a client sees for one record they have access to.</summary>
public sealed class BurialSiteDto
{
    public long Id { get; init; }
    public long CemeteryId { get; init; }
    public string? CemeteryName { get; init; }
    public string DeceasedFullName { get; init; } = string.Empty;
    public string? BirthDateText { get; init; }
    public string? DeathDateText { get; init; }
    public string? PlotSection { get; init; }
    public string? Landmarks { get; init; }
    public decimal? GeoLat { get; init; }
    public decimal? GeoLng { get; init; }
    public string? Notes { get; init; }
    public string LocationQuality { get; init; } = string.Empty;

    /// <summary>What the caller may do with this record — "view", "order" or "manage".
    /// The owner always gets "manage". The frontend uses this to decide whether to show
    /// the order button, but every write is re-checked server-side regardless.</summary>
    public string Permission { get; init; } = string.Empty;

    public bool IsOwner { get; init; }
    public int PhotoCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateBurialSiteDto
{
    [Required]
    public long CemeteryId { get; init; }

    [Required]
    [StringLength(255, MinimumLength = 2)]
    public string DeceasedFullName { get; init; } = string.Empty;

    [StringLength(64)]
    public string? BirthDateText { get; init; }

    [StringLength(64)]
    public string? DeathDateText { get; init; }

    [StringLength(128)]
    public string? PlotSection { get; init; }

    [StringLength(2000)]
    public string? Landmarks { get; init; }

    [Range(-90, 90)]
    public decimal? GeoLat { get; init; }

    [Range(-180, 180)]
    public decimal? GeoLng { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}

/// <summary>Every field is optional: this is a partial update, and null means "leave alone".
/// The cemetery cannot be changed — moving a record between cemeteries would invalidate its
/// visit history, so that is a delete-and-recreate, not an edit.</summary>
public sealed class UpdateBurialSiteDto
{
    [StringLength(255, MinimumLength = 2)]
    public string? DeceasedFullName { get; init; }

    [StringLength(64)]
    public string? BirthDateText { get; init; }

    [StringLength(64)]
    public string? DeathDateText { get; init; }

    [StringLength(128)]
    public string? PlotSection { get; init; }

    [StringLength(2000)]
    public string? Landmarks { get; init; }

    [Range(-90, 90)]
    public decimal? GeoLat { get; init; }

    [Range(-180, 180)]
    public decimal? GeoLng { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class CemeteryDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Region { get; init; }
    public string? Address { get; init; }
    public string? Hours { get; init; }
}

public sealed class BurialSiteMemberDto
{
    public long Id { get; init; }
    public long? UserId { get; init; }

    /// <summary>Masked for everyone but the person who sent the invite — a member list should
    /// not hand out other relatives' full contact details.</summary>
    public string? Contact { get; init; }

    public string Permission { get; init; } = string.Empty;
    public bool Accepted { get; init; }
    public DateTimeOffset? AcceptedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class InviteMemberDto
{
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Contact { get; init; } = string.Empty;

    /// <summary>"view", "order" or "manage". Defaults to view — the ability to spend money is
    /// never granted implicitly.</summary>
    public string Permission { get; init; } = Models.BurialSites.BurialSitePermissions.View;
}

public sealed class AcceptInvitationDto
{
    [Required]
    public string Token { get; init; } = string.Empty;
}
