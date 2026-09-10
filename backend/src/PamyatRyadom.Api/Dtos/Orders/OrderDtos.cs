using System.ComponentModel.DataAnnotations;

namespace PamyatRyadom.Api.Dtos.Orders;

public sealed class OrderSummaryDto
{
    public long Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>What the client is told. Internal statuses describe our process, not their
    /// situation — see OrderStatusPresentation.</summary>
    public string StatusLabel { get; init; } = string.Empty;

    /// <summary>The one action expected of them right now, or null when the ball is on our side.</summary>
    public string? Cta { get; init; }

    public string PackageTitle { get; init; } = string.Empty;
    public string DeceasedFullName { get; init; } = string.Empty;
    public DateTimeOffset? PreferredFrom { get; init; }
    public DateTimeOffset? PreferredTo { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Which pile this belongs in on the staff queue: "staff", "customer", "in_flight"
    /// or "done". Server-side so the queue UI cannot classify a status differently from the
    /// service that selected it.</summary>
    public string QueueGroup { get; init; } = string.Empty;
}

public sealed class OrderDto
{
    public long Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string? Cta { get; init; }

    public long BurialSiteId { get; init; }
    public string DeceasedFullName { get; init; } = string.Empty;

    // The package as sold — a snapshot, not a live read of the catalogue.
    public string PackageCode { get; init; } = string.Empty;
    public string PackageVersion { get; init; } = string.Empty;
    public string PackageTitle { get; init; } = string.Empty;
    public decimal PackagePriceFromRub { get; init; }
    public int WarrantyDays { get; init; }
    public DateTimeOffset? WarrantyUntil { get; init; }

    public DateTimeOffset? PreferredFrom { get; init; }
    public DateTimeOffset? PreferredTo { get; init; }
    public string? Comment { get; init; }
    public string? CancellationReason { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Everything the client may see: never a draft, which is the dispatcher's workings.</summary>
    public IReadOnlyList<EstimateDto> Estimates { get; init; } = Array.Empty<EstimateDto>();

    public IReadOnlyList<OrderHistoryDto> History { get; init; } = Array.Empty<OrderHistoryDto>();
}

public sealed class OrderHistoryDto
{
    public string ToStatus { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class EstimateDto
{
    public int Version { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal TotalRub { get; init; }
    public DateTimeOffset? ValidUntil { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset? AcceptedAt { get; init; }
    public DateTimeOffset? RejectedAt { get; init; }
    public IReadOnlyList<EstimateLineDto> Lines { get; init; } = Array.Empty<EstimateLineDto>();
}

public sealed class EstimateLineDto
{
    /// <summary>work | material | discount | delivery | extra</summary>
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPriceRub { get; init; }
    public decimal TotalRub { get; init; }
}

public sealed class CreateOrderDto
{
    [Required]
    public long BurialSiteId { get; init; }

    [Required]
    public string PackageCode { get; init; } = string.Empty;

    public DateTimeOffset? PreferredFrom { get; init; }
    public DateTimeOffset? PreferredTo { get; init; }

    [StringLength(2000)]
    public string? Comment { get; init; }

    public string? Source { get; init; }
}

public sealed class AcceptEstimateDto
{
    /// <summary>Required, never inferred: agreeing to "the current estimate" is not agreeing to a
    /// set of lines (BR-002).</summary>
    [Required]
    public int Version { get; init; }
}

public sealed class RejectEstimateDto
{
    [Required]
    public int Version { get; init; }

    [StringLength(1000)]
    public string? Reason { get; init; }
}

public sealed class CancelOrderDto
{
    [StringLength(1000)]
    public string? Reason { get; init; }
}

public sealed class SaveEstimateDto
{
    [StringLength(2000)]
    public string? Note { get; init; }

    /// <summary>After this the quote must be re-confirmed: real costs move with the season, and a
    /// quote held for months is not one the operator can honour.</summary>
    public DateTimeOffset? ValidUntil { get; init; }

    public List<SaveEstimateLineDto>? Lines { get; init; }
}

public sealed class SaveEstimateLineDto
{
    /// <summary>work | material | discount | delivery | extra</summary>
    [Required]
    public string Type { get; init; } = "work";

    [Required]
    [StringLength(255)]
    public string Title { get; init; } = string.Empty;

    public decimal Quantity { get; init; } = 1m;

    [StringLength(32)]
    public string? Unit { get; init; }

    /// <summary>Negative only on a discount line.</summary>
    public decimal UnitPriceRub { get; init; }
}
