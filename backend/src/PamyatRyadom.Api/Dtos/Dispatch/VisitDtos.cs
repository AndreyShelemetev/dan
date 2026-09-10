namespace PamyatRyadom.Api.Dtos.Dispatch;

/// <summary>
/// A visit as staff and executors see it.
///
/// <see cref="PayoutRub"/> lives here and only here. There is a separate client-facing shape
/// below precisely so the payout cannot reach a client by someone adding a field to the wrong
/// class.
/// </summary>
public sealed class VisitDto
{
    public long Id { get; init; }
    public long OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;

    public long? ExecutorUserId { get; init; }
    public string? ExecutorName { get; init; }

    public DateTimeOffset? ScheduledFor { get; init; }
    public DateTimeOffset? OfferExpiresAt { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }

    public string? ExecutorNote { get; init; }
    public string? ReviewNote { get; init; }
    public string? DeclineReason { get; init; }

    /// <summary>Executor payout. Commercially confidential — staff and the executor themselves.</summary>
    public decimal? PayoutRub { get; init; }

    /// <summary>Where the work is. The executor needs it to get there.</summary>
    public string DeceasedFullName { get; init; } = string.Empty;
    public string? CemeteryName { get; init; }
    public string? PlotSection { get; init; }
    public string? Landmarks { get; init; }

    public IReadOnlyList<ChecklistItemDto> Checklist { get; init; } = Array.Empty<ChecklistItemDto>();
}

/// <summary>
/// The report as the client sees it.
///
/// A separate type rather than a filtered <see cref="VisitDto"/>: the payout must be impossible
/// to leak here, and "impossible" means the field does not exist, not that it is set to null.
/// QA's note is absent for the same reason — it is feedback to an executor and reads as an
/// accusation to a client.
/// </summary>
public sealed class VisitReportDto
{
    public long Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public DateTimeOffset? VisitedOn { get; init; }

    /// <summary>What the executor found and did, in their words.</summary>
    public string? Note { get; init; }

    public IReadOnlyList<ChecklistItemDto> Checklist { get; init; } = Array.Empty<ChecklistItemDto>();
}

public sealed class ChecklistItemDto
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public bool Optional { get; init; }
    public string Result { get; init; } = string.Empty;
    public string ResultLabel { get; init; } = string.Empty;
    public string? Note { get; init; }
    public int SortOrder { get; init; }
}

public sealed class AssignVisitDto
{
    public long ExecutorUserId { get; init; }
    public DateTimeOffset? ScheduledFor { get; init; }

    /// <summary>What the executor is paid. Set by dispatch; never derived from the order price in
    /// code, because the margin is a commercial decision, not a formula.</summary>
    public decimal? PayoutRub { get; init; }

    /// <summary>Hours the offer stands before it returns to the queue. Silence is an expiry.</summary>
    public int OfferHours { get; init; } = 24;
}

public sealed class ChecklistAnswerDto
{
    public string Key { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public sealed class SubmitReportDto
{
    /// <summary>Client-facing note. Payout figures must not appear here — the service strips
    /// nothing, so this is a rule the UI and the reviewer enforce.</summary>
    public string? Note { get; init; }

    public IReadOnlyList<ChecklistAnswerDto> Checklist { get; init; } = Array.Empty<ChecklistAnswerDto>();
}

public sealed class ReviewDto
{
    public string? Note { get; init; }
}

/// <summary>An executor as a name to pick from. Nothing else: an assignment picker does not need
/// their contact details, and a DTO that carries them is one careless render from leaking them.</summary>
public sealed class ExecutorOptionDto
{
    public long Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
