namespace PamyatRyadom.Api.Models.BurialSites;

/// <summary>
/// The digital record of one place of remembrance — the reason a client creates an account.
///
/// Explicitly NOT an official burial registry and NOT proof of any right to the plot: the
/// product promise and the legal texts both depend on that distinction, so it is stated here
/// as well as in the UI copy.
/// </summary>
public sealed class BurialSite
{
    public long Id { get; set; }
    public long CemeteryId { get; set; }

    /// <summary>The account that created the record and always retains full control over it.
    /// Relatives get access through <see cref="BurialSiteMember"/> instead.</summary>
    public long OwnerUserId { get; set; }

    public string DeceasedFullName { get; set; } = string.Empty;

    /// <summary>Life dates are text, not dates, on purpose: relatives frequently know only a
    /// year, only a month, or a date that disagrees with the headstone. Forcing a valid
    /// calendar date here would make people invent one, which is worse than storing what
    /// they actually know.</summary>
    public string? BirthDateText { get; set; }
    public string? DeathDateText { get; set; }

    /// <summary>Plot addressing as the cemetery itself uses it (section / row / plot).</summary>
    public string? PlotSection { get; set; }

    /// <summary>Free-text landmarks ("third row from the chapel, blue fence"). In practice
    /// this is what actually lets an executor find the grave.</summary>
    public string? Landmarks { get; set; }

    public decimal? GeoLat { get; set; }
    public decimal? GeoLng { get; set; }

    /// <summary>Client's standing instructions: religious restrictions, what must never be
    /// removed, plants not to cut. Passed to the executor with the assignment.</summary>
    public string? Notes { get; set; }

    /// <summary>Whether the stored description is good enough to send an executor to.
    /// Drives the LOCATION_REVIEW branch of the order flow: when this is
    /// <see cref="LocationQualities.Insufficient"/> the service sells an inspection rather
    /// than a full package, instead of dispatching someone to search blind.</summary>
    public string LocationQuality { get; set; } = LocationQualities.Unverified;

    public string Status { get; set; } = BurialSiteStatuses.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Cemetery? Cemetery { get; set; }
    public ICollection<BurialSiteMember> Members { get; set; } = new List<BurialSiteMember>();
}

public static class LocationQualities
{
    /// <summary>Not yet assessed by staff — the default for a freshly created record.</summary>
    public const string Unverified = "unverified";

    /// <summary>Enough to dispatch an executor.</summary>
    public const string Sufficient = "sufficient";

    /// <summary>Too vague to find; an inspection is required before any work is sold.</summary>
    public const string Insufficient = "insufficient";

    public static readonly IReadOnlyCollection<string> All = new[] { Unverified, Sufficient, Insufficient };
}

public static class BurialSiteStatuses
{
    public const string Active = "active";

    /// <summary>Soft delete: the repo convention is a status value, never a deleted_at column,
    /// so that history and past orders stay intact.</summary>
    public const string Deleted = "deleted";

    public static readonly IReadOnlyCollection<string> All = new[] { Active, Deleted };
}
