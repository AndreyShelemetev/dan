namespace PamyatRyadom.Api.Models.Catalog;

/// <summary>
/// A plan of scheduled visits sold as one purchase.
///
/// Versioned on the same discipline as <see cref="ServicePackage"/>, and for a sharper reason: a
/// subscription is a commitment stretching over months, so the terms a client bought under have
/// to survive every price change that happens while it runs. A subscription in flight reads its
/// own version, never "the current plan".
/// </summary>
public sealed class SubscriptionPlan
{
    public long Id { get; set; }

    /// <summary>Stable identity across versions: "care-2", "care-4", "care-6".</summary>
    public string Code { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0";
    public string Locale { get; set; } = "ru";

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    /// <summary>Which package each scheduled visit is performed to. Bound by code rather than by
    /// id: the plan follows the package's current published version at the moment a visit is
    /// created, which is what lets the checklist improve without reissuing every subscription.</summary>
    public string ServicePackageCode { get; set; } = ServicePackageCodes.Basic;

    /// <summary>Visits included in one period.</summary>
    public int VisitsTotal { get; set; }

    /// <summary>Length of the period in months — 12 for a season, 6 for a half-year.</summary>
    public int PeriodMonths { get; set; }

    public decimal PriceRub { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = ServicePackageStatuses.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Price of one visit under this plan, for the "выгоднее на N%" line. Computed, never
    /// stored: a stored copy is one more thing that can disagree with the price.</summary>
    public decimal PricePerVisit => VisitsTotal > 0 ? decimal.Round(PriceRub / VisitsTotal, 2) : 0m;
}
