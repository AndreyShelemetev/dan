namespace PamyatRyadom.Api.Dtos.Disputes;

/// <summary>
/// A dispute as the client who opened it sees it: their own reason, the current status, and once
/// decided, the resolution. No staff-only fields — who is handling it internally is not the
/// client's business, only the outcome is.
/// </summary>
public sealed class DisputeDto
{
    public long Id { get; init; }
    public long OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;

    public string? ResolutionType { get; init; }
    public string? ResolutionTypeLabel { get; init; }
    public string? ResolutionText { get; init; }

    /// <summary>Set only for a partial refund. A full refund's amount lives on the payment
    /// record, not duplicated here.</summary>
    public decimal? RefundAmountRub { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// A dispute as support/finance/admin see it while working the queue — the same facts as
/// <see cref="DisputeDto"/>, plus who opened and (once decided) who resolved it and which order
/// it belongs to. Still nothing about the executor's payout: that stays out of dispute text on
/// every surface, staff included, same as it stays out of the client's.
/// </summary>
public sealed class AdminDisputeDto
{
    public long Id { get; init; }
    public long OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;

    public string? ResolutionType { get; init; }
    public string? ResolutionTypeLabel { get; init; }
    public string? ResolutionText { get; init; }
    public decimal? RefundAmountRub { get; init; }

    public long OpenedByUserId { get; init; }
    public long? ResolvedByUserId { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Resolving with no money involved: rework or a plain rejection.</summary>
public sealed class ResolveDisputeDto
{
    /// <summary>"rework" or "rejected" — anything else, including a refund type, is refused on
    /// purpose. A refund goes through the separate refund endpoint instead.</summary>
    public string ResolutionType { get; init; } = string.Empty;

    public string? ResolutionText { get; init; }
}

/// <summary>Resolving with a refund — the one path that actually moves money.</summary>
public sealed class RefundDisputeDto
{
    /// <summary>Roubles to return. Null refunds everything still owed on the order.</summary>
    public decimal? AmountRub { get; init; }

    public string? ResolutionText { get; init; }
}
