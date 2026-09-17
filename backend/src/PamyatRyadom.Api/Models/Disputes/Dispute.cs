namespace PamyatRyadom.Api.Models.Disputes;

/// <summary>
/// A client complaint about an order, from the moment it is raised to the moment it is resolved.
///
/// The order status `disputed` says a complaint exists; this is the case behind it — who opened
/// it, why, and what support/admin decided. Exactly one live dispute per order (enforced by a
/// partial unique index): a second complaint about the same work is the same case, not a new one.
/// </summary>
public sealed class Dispute
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    /// <summary>The client who opened it. Always the order's own customer — a dispute is not
    /// something staff files on a client's behalf.</summary>
    public long OpenedByUserId { get; set; }

    /// <summary>The client's own words. Required: a complaint with no statement of what is wrong
    /// cannot be resolved, only argued about.</summary>
    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = DisputeStatuses.Open;

    /// <summary>Set only once the dispute reaches <see cref="DisputeStatuses.Resolved"/> or
    /// <see cref="DisputeStatuses.Rejected"/>.</summary>
    public string? ResolutionType { get; set; }

    /// <summary>Why that decision, in support/admin's words. Required whenever a decision is
    /// recorded — a resolution with no explanation is not a resolution.</summary>
    public string? ResolutionText { get; set; }

    /// <summary>Amount refunded, when the resolution is a partial refund. Never set for a full
    /// refund — that amount is on the payment record, not duplicated here.</summary>
    public decimal? RefundAmountRub { get; set; }

    public long? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Orders.Order? Order { get; set; }
}

public static class DisputeStatuses
{
    /// <summary>Just opened, nobody has picked it up yet.</summary>
    public const string Open = "open";

    /// <summary>Support or admin is looking into it.</summary>
    public const string InReview = "in_review";

    /// <summary>A decision was made in the client's favour (rework or a refund).</summary>
    public const string Resolved = "resolved";

    /// <summary>The complaint was looked at and declined.</summary>
    public const string Rejected = "rejected";

    public static readonly IReadOnlyCollection<string> All = new[] { Open, InReview, Resolved, Rejected };

    /// <summary>A dispute in either of these states is "live" — the one a new complaint about the
    /// same order would collide with.</summary>
    public static readonly IReadOnlyCollection<string> Live = new[] { Open, InReview };

    public static readonly IReadOnlyCollection<string> Decided = new[] { Resolved, Rejected };
}

public static class DisputeResolutionTypes
{
    /// <summary>Send the executor back to redo the work.</summary>
    public const string Rework = "rework";

    /// <summary>Some of the money back; the order is not moved to `refunded` (the work still
    /// happened).</summary>
    public const string PartialRefund = "partial_refund";

    /// <summary>All of the money back; moves the order to `refunded`.</summary>
    public const string FullRefund = "full_refund";

    /// <summary>The complaint was heard and declined.</summary>
    public const string Rejected = "rejected";

    public static readonly IReadOnlyCollection<string> All = new[] { Rework, PartialRefund, FullRefund, Rejected };
}
