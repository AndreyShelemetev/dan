using System.Text.Json;

namespace PamyatRyadom.Api.Models.BurialSites;

/// <summary>
/// A cemetery the service operates at. Reference data curated by staff, not by clients:
/// the pilot runs in a handful of clusters, and dispatch relies on the rules/hours here
/// when planning a visit.
/// </summary>
public sealed class Cemetery
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? Address { get; set; }

    /// <summary>Approximate entrance/centre point, for routing and clustering — not for
    /// navigating to an individual plot, which cemetery paths do not support anyway.</summary>
    public decimal? GeoLat { get; set; }
    public decimal? GeoLng { get; set; }

    /// <summary>Opening hours as free text: real cemeteries publish these in wildly
    /// inconsistent formats, and a structured schedule would lose more than it gains.</summary>
    public string? Hours { get; set; }

    /// <summary>Site rules an executor must respect (permitted works, banned chemicals,
    /// access restrictions). Shown to the executor before the visit starts.</summary>
    public string? Rules { get; set; }

    /// <summary>Administration contacts, stored as JSON so the shape can grow without a
    /// migration. Staff-facing only — never exposed to clients.</summary>
    public JsonDocument? Contacts { get; set; }

    public string Status { get; set; } = CemeteryStatuses.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<BurialSite> BurialSites { get; set; } = new List<BurialSite>();
}

public static class CemeteryStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlyCollection<string> All = new[] { Active, Archived };
}
