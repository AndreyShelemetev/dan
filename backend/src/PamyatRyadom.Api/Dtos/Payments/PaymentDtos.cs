namespace PamyatRyadom.Api.Dtos.Payments;

public sealed class PaymentDto
{
    public long Id { get; init; }
    public long OrderId { get; init; }
    public string Status { get; init; } = string.Empty;

    /// <summary>What the client is told. Provider vocabulary ("waiting_for_capture") describes
    /// their process, not the client's situation.</summary>
    public string StatusLabel { get; init; } = string.Empty;

    public decimal AmountRub { get; init; }
    public decimal RefundedRub { get; init; }
    public int EstimateVersion { get; init; }

    /// <summary>Where to send the client to pay. Null once there is nothing left to do.</summary>
    public string? ConfirmationUrl { get; init; }

    public DateTimeOffset? PaidAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>True while this is the attempt the client should be finishing. The provider and
    /// its internal id are deliberately absent: neither is any of the browser's business.</summary>
    public bool IsActive { get; init; }
}

public sealed class RefundRequestDto
{
    /// <summary>Roubles to return. Null refunds the whole payment — the common case, and one
    /// worth not making a caller compute.</summary>
    public decimal? AmountRub { get; init; }

    public string? Reason { get; init; }
}
