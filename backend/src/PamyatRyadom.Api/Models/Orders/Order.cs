using PamyatRyadom.Api.Models.BurialSites;

namespace PamyatRyadom.Api.Models.Orders;

/// <summary>
/// One agreed set of works, for one burial site, in one service window (BR-001).
///
/// The package is copied in rather than referenced by code: the snapshot fields below are what
/// was actually sold, and they must survive every later catalogue edit. A price change next month
/// cannot be allowed to rewrite what this client bought — that is the whole reason the catalogue
/// is versioned, and this is where the version is pinned.
/// </summary>
public sealed class Order
{
    public long Id { get; set; }

    /// <summary>Human-facing number. Used in support conversations and on the receipt, because
    /// nobody reads a database id aloud over the phone.</summary>
    public string Number { get; set; } = string.Empty;

    public long CustomerUserId { get; set; }
    public long BurialSiteId { get; set; }

    // -- Package snapshot -------------------------------------------------------------------------

    /// <summary>Points at the exact catalogue row this was sold under.</summary>
    public long ServicePackageId { get; set; }

    public string PackageCode { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public string PackageTitle { get; set; } = string.Empty;

    /// <summary>The "from" price at the moment of sale. Not the amount owed — that comes from the
    /// accepted estimate — but the figure the client was shown when they chose.</summary>
    public decimal PackagePriceFromRub { get; set; }

    /// <summary>Warranty length copied from the package version, so shortening it in the
    /// catalogue cannot retroactively close a window a client still has (BR-012).</summary>
    public int WarrantyDays { get; set; }

    // -- Lifecycle --------------------------------------------------------------------------------

    /// <summary>Changed only through <see cref="OrderStateMachine"/>.</summary>
    public string Status { get; set; } = OrderStatuses.Draft;

    /// <summary>Preferred window, as the client asked for it. Not a promise: the interface says
    /// so explicitly, because a slot is only real once operations confirm it (FR-CUS-007).</summary>
    public DateTimeOffset? PreferredFrom { get; set; }
    public DateTimeOffset? PreferredTo { get; set; }

    public string? CustomerComment { get; set; }

    /// <summary>Where the order came from, for the funnel. Never used to price anything.</summary>
    public string? Source { get; set; }

    /// <summary>Set when a status change closes the order, for the reason the client is shown.</summary>
    public string? CancellationReason { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public BurialSite? BurialSite { get; set; }
    public ICollection<Estimate> Estimates { get; set; } = new List<Estimate>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();

    /// <summary>The warranty deadline, once the work has been accepted.</summary>
    public DateTimeOffset? WarrantyUntil =>
        CompletedAt is null ? null : CompletedAt.Value.AddDays(WarrantyDays);
}

/// <summary>
/// Append-only record of every status change.
///
/// Kept because "when did this order become paid, and who moved it" is a question asked during a
/// dispute, and reconstructing it from application logs is not an answer anyone can rely on.
/// Also the audit surface for FR-ADM-019: a manual override records its reason here.
/// </summary>
public sealed class OrderStatusHistory
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    /// <summary>Null on creation, when there is no previous state.</summary>
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;

    /// <summary>Null when the change was made by the system rather than a person — a payment
    /// confirmation or an expiry, for instance.</summary>
    public long? ActorUserId { get; set; }
    public string? ActorRole { get; set; }

    /// <summary>Mandatory for an administrative override (BR-018); optional otherwise.</summary>
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Order? Order { get; set; }
}
