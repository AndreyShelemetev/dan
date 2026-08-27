namespace PamyatRyadom.Api.Models.Orders;

/// <summary>
/// The allowed transitions, and the only authority on them.
///
/// Every status change goes through here. Nothing writes <c>order.Status</c> directly — not a
/// controller, not a background job, not a repair script — because a state machine with a second
/// door is not a state machine, and the first thing lost is the ability to say what a given order
/// can still legally do.
///
/// The table below is the product's process written down. It is worth reading as a specification
/// rather than as configuration: what it forbids is as load-bearing as what it allows.
/// </summary>
public static class OrderStateMachine
{
    private static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>
    {
        // Nothing is sold yet; the client may still walk away freely.
        [OrderStatuses.Draft] = new[] { OrderStatuses.Submitted, OrderStatuses.Cancelled },

        // Operations triage: is the place findable from what we were given?
        [OrderStatuses.Submitted] = new[]
        {
            OrderStatuses.LocationReview, OrderStatuses.EstimateReady, OrderStatuses.Cancelled,
        },

        [OrderStatuses.LocationReview] = new[] { OrderStatuses.EstimateReady, OrderStatuses.Cancelled },

        // Re-entrant on purpose: publishing a corrected estimate lands here again, and the
        // client's previous acceptance is invalidated with it (BR-002).
        [OrderStatuses.EstimateReady] = new[]
        {
            OrderStatuses.EstimateReady, OrderStatuses.AwaitingPayment, OrderStatuses.Cancelled,
        },

        // Only a verified provider confirmation moves this forward — never a browser redirect.
        [OrderStatuses.AwaitingPayment] = new[]
        {
            OrderStatuses.Paid, OrderStatuses.EstimateReady, OrderStatuses.Cancelled,
        },

        [OrderStatuses.Paid] = new[]
        {
            OrderStatuses.Assigning, OrderStatuses.Cancelled, OrderStatuses.Refunded,
        },

        [OrderStatuses.Assigning] = new[]
        {
            OrderStatuses.Assigned, OrderStatuses.Cancelled, OrderStatuses.Refunded,
        },

        // Back to assigning when an executor declines or an assignment is revoked — silence is
        // an expiry, never an acceptance (BR-005).
        [OrderStatuses.Assigned] = new[]
        {
            OrderStatuses.InProgress, OrderStatuses.Assigning, OrderStatuses.Cancelled,
        },

        // Cancellation from here is the emergency path only: wrong grave, danger on site, a ban
        // from the administration.
        [OrderStatuses.InProgress] = new[]
        {
            OrderStatuses.ExtraApproval, OrderStatuses.QaReview, OrderStatuses.Cancelled,
        },

        [OrderStatuses.ExtraApproval] = new[]
        {
            OrderStatuses.InProgress, OrderStatuses.QaReview, OrderStatuses.Cancelled,
        },

        // QA sends work back rather than publishing it. There is no path from here straight to
        // the client (BR-010).
        [OrderStatuses.QaReview] = new[]
        {
            OrderStatuses.InProgress, OrderStatuses.CustomerReview, OrderStatuses.Disputed,
        },

        [OrderStatuses.CustomerReview] = new[] { OrderStatuses.Completed, OrderStatuses.Disputed },

        // Completed is not quite the end: the warranty window still allows a complaint (BR-012).
        [OrderStatuses.Completed] = new[] { OrderStatuses.Disputed },

        [OrderStatuses.Disputed] = new[]
        {
            OrderStatuses.Completed, OrderStatuses.InProgress, OrderStatuses.Refunded, OrderStatuses.Cancelled,
        },

        [OrderStatuses.Cancelled] = Array.Empty<string>(),
        [OrderStatuses.Refunded] = Array.Empty<string>(),
    };

    public static IReadOnlyCollection<string> NextFrom(string status) =>
        Allowed.TryGetValue(status, out var next) ? next : Array.Empty<string>();

    public static bool CanTransition(string from, string to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>
    /// Whether money has been taken for this order.
    ///
    /// Used to decide whether cancelling has to go through a refund rather than simply closing
    /// the order — the two look the same to a client and are entirely different to the finance
    /// side.
    /// </summary>
    public static bool IsPaidStage(string status) => status is
        OrderStatuses.Paid or OrderStatuses.Assigning or OrderStatuses.Assigned or
        OrderStatuses.InProgress or OrderStatuses.ExtraApproval or OrderStatuses.QaReview or
        OrderStatuses.CustomerReview or OrderStatuses.Completed or OrderStatuses.Disputed;

    /// <summary>Whether the client may still edit the order's own fields (window, comment).
    /// After an estimate is published, changes belong in a new estimate version instead.</summary>
    public static bool IsClientEditable(string status) => status is
        OrderStatuses.Draft or OrderStatuses.Submitted or OrderStatuses.LocationReview;
}
