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
