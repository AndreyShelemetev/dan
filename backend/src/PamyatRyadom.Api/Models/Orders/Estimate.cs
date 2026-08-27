namespace PamyatRyadom.Api.Models.Orders;

/// <summary>
/// A priced, line-by-line quote for one order, at one version.
///
/// Versioned because acceptance is version-specific: the client agrees to a set of lines, and
/// changing any of them invalidates that agreement (BR-002). Re-pricing therefore means a new
/// version, published and accepted afresh — never an edit to something already agreed.
///
/// This is the mechanism behind the product's central promise, "без доплат без вашего согласия".
/// It is not bookkeeping detail; it is the feature.
/// </summary>
public sealed class Estimate
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    /// <summary>1, 2, 3… within one order.</summary>
    public int Version { get; set; }

    public string Status { get; set; } = EstimateStatuses.Draft;

    /// <summary>Sum of the lines. Stored rather than computed on read so the accepted figure is
    /// fixed at the moment of acceptance — recomputing later would silently follow any change.</summary>
    public decimal TotalRub { get; set; }

    /// <summary>After this, the quote has to be re-confirmed. Real costs move with the season and
    /// a quote held for months is not one the operator can honour.</summary>
    public DateTimeOffset? ValidUntil { get; set; }

    /// <summary>Free-text note from the dispatcher shown above the lines — why this scope, what
    /// was found. Client-facing, so it carries no payout figures.</summary>
    public string? Note { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Who published it — a dispatcher, for the audit trail.</summary>
    public long? PublishedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Order? Order { get; set; }
    public ICollection<EstimateLine> Lines { get; set; } = new List<EstimateLine>();
}

public static class EstimateStatuses
{
    /// <summary>Being prepared by a dispatcher; the client cannot see it.</summary>
    public const string Draft = "draft";

    /// <summary>Sent to the client and awaiting their decision. Immutable from here.</summary>
    public const string Published = "published";

    public const string Accepted = "accepted";
    public const string Rejected = "rejected";

    /// <summary>Replaced by a newer version. Kept, never deleted: it is evidence of what was
    /// offered and when.</summary>
    public const string Superseded = "superseded";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Draft, Published, Accepted, Rejected, Superseded,
    };
}

/// <summary>
/// One line of a quote.
///
/// Materials are separate lines rather than folded into the work price, because the business
/// rule is that a client sees quantity and unit price for anything bought on their behalf. A
/// single opaque "работы и материалы" figure is exactly the shape of the hidden-extras problem
/// this product exists to answer.
/// </summary>
public sealed class EstimateLine
{
    public long Id { get; set; }
    public long EstimateId { get; set; }

    public string Type { get; set; } = EstimateLineTypes.Work;

    public string Title { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;

    /// <summary>"шт", "м²", "мешок" — shown next to the quantity.</summary>
    public string? Unit { get; set; }

    /// <summary>Price of one unit, in roubles. Negative only on a discount line.</summary>
    public decimal UnitPriceRub { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Estimate? Estimate { get; set; }

    /// <summary>Line total. Computed, never stored: a stored copy is one more number that can
    /// disagree with the two it comes from.</summary>
    public decimal TotalRub => decimal.Round(Quantity * UnitPriceRub, 2);
}

public static class EstimateLineTypes
{
    public const string Work = "work";
    public const string Material = "material";

    /// <summary>Carries a negative unit price; the only line type allowed to.</summary>
    public const string Discount = "discount";

    public const string Delivery = "delivery";

    /// <summary>Work found on site and agreed separately (BR-004).</summary>
    public const string Extra = "extra";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Work, Material, Discount, Delivery, Extra,
    };
}
