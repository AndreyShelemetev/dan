namespace PamyatRyadom.Api.Models.Dispatch;

/// <summary>
/// One executor's trip to one grave, and the report that comes back from it.
///
/// The report is the product. A client who cannot go to the cemetery is buying evidence that
/// somebody did, so the photographs are not a nicety attached to the work — they are the
/// deliverable, and this is the record that says whether they arrived.
///
/// The checklist is copied onto the visit at assignment rather than read from the catalogue at
/// review time. QA measures the work against what was promised when it was sold; a checklist
/// still pointing at a live catalogue row would let an edit move that line afterwards.
/// </summary>
public sealed class Visit
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    /// <summary>Null while the work is offered but unclaimed. An executor is assigned by
    /// dispatch, never chosen by the client — this is a managed service, not a marketplace.</summary>
    public long? ExecutorUserId { get; set; }

    public string Status { get; set; } = VisitStatuses.Offered;

    /// <summary>When the executor is expected on site. A date, not a promise of an hour: a
    /// cemetery visit is weather- and access-dependent and a false precision here is a complaint
    /// waiting to happen.</summary>
    public DateTimeOffset? ScheduledFor { get; set; }

    /// <summary>How long the offer stands before it goes back to the queue. Silence is an expiry,
    /// never an acceptance (BR-005).</summary>
    public DateTimeOffset? OfferExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the executor filed the report — not when the work was approved.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }

    /// <summary>Why QA sent it back, in QA's words. Internal: it is feedback to an executor, and
    /// reads as an accusation if it reaches the client.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Why the executor could not take or finish it.</summary>
    public string? DeclineReason { get; set; }

    /// <summary>What the executor is paid, in roubles.
    ///
    /// Commercially confidential. It must never appear in a client-facing DTO, a dispute, or a
    /// notification — the margin between this and the order price is the operator's business and
    /// nobody else's.</summary>
    public decimal? PayoutRub { get; set; }

    /// <summary>The executor's note to the client: what was found, what was done. Client-facing,
    /// so it carries no payout figures.</summary>
    public string? ExecutorNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Orders.Order? Order { get; set; }
    public ICollection<VisitChecklistItem> ChecklistItems { get; set; } = new List<VisitChecklistItem>();
}

public static class VisitStatuses
{
    /// <summary>Offered to an executor and waiting for them to take it.</summary>
    public const string Offered = "offered";

    public const string Accepted = "accepted";

    /// <summary>The executor turned it down, or let the offer expire. Terminal for this visit;
    /// dispatch creates another.</summary>
    public const string Declined = "declined";

    /// <summary>On site, work started.</summary>
    public const string InProgress = "in_progress";

    /// <summary>Report filed and waiting for QA. The executor can no longer change it — a report
    /// editable after submission is not evidence of anything.</summary>
    public const string Submitted = "submitted";

    /// <summary>QA sent it back with a note. The executor may re-file.</summary>
    public const string Rework = "rework";

    /// <summary>QA approved it. Only now may the client see it (BR-010).</summary>
    public const string Approved = "approved";

    /// <summary>Could not be done: no access, wrong grave, danger on site.</summary>
    public const string Failed = "failed";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Offered, Accepted, Declined, InProgress, Submitted, Rework, Approved, Failed,
    };

    /// <summary>States an executor may still act from.</summary>
    public static bool IsExecutorActionable(string status) => status is
        Offered or Accepted or InProgress or Rework;
}

/// <summary>
/// One line of the checklist, as copied onto a visit and answered by the executor.
///
/// Every item gets an answer. "Not done" with a reason is a legitimate outcome — a tap frozen, a
/// grave under snow — and forcing a reason is what stops it becoming a silent omission that
/// nobody notices until the client asks (FR-EXE-009).
/// </summary>
public sealed class VisitChecklistItem
{
    public long Id { get; set; }
    public long VisitId { get; set; }

    /// <summary>Stable key from the package's checklist template. Survives a retitle.</summary>
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Whether the template allowed skipping this one.</summary>
    public bool Optional { get; set; }

    public string Result { get; set; } = ChecklistResults.Pending;

    /// <summary>Required for anything but <see cref="ChecklistResults.Done"/>.</summary>
    public string? Note { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Visit? Visit { get; set; }
}

public static class ChecklistResults
{
    public const string Pending = "pending";
    public const string Done = "done";

    /// <summary>Attempted and could not be: frozen tap, locked gate, snow.</summary>
    public const string Impossible = "impossible";

    /// <summary>Did not apply on the day: nothing to clear, no flowers to replace.</summary>
    public const string NotRequired = "not_required";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Pending, Done, Impossible, NotRequired,
    };

    /// <summary>Whether this answer has to be explained. Anything but "done" does.</summary>
    public static bool NeedsNote(string result) => result is Impossible or NotRequired;
}
