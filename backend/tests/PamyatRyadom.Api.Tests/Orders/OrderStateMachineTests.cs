using PamyatRyadom.Api.Models.Orders;

namespace PamyatRyadom.Api.Tests.Orders;

/// <summary>
/// The transition table, tested as a specification.
///
/// What it forbids matters as much as what it allows, so the forbidden cases are enumerated
/// explicitly rather than assumed. The failure these guard against is quiet: a new feature adds
/// one convenient shortcut, and six months later nobody can say what an order in a given state
/// is still allowed to do.
///
/// No database and no host — this is pure domain logic and testing it through HTTP would only
/// make the failures harder to read.
/// </summary>
public sealed class OrderStateMachineTests
{
    [Theory]
    // The ordinary path, start to finish.
    [InlineData(OrderStatuses.Draft, OrderStatuses.Submitted)]
    [InlineData(OrderStatuses.Submitted, OrderStatuses.EstimateReady)]
    [InlineData(OrderStatuses.EstimateReady, OrderStatuses.AwaitingPayment)]
    [InlineData(OrderStatuses.AwaitingPayment, OrderStatuses.Paid)]
    [InlineData(OrderStatuses.Paid, OrderStatuses.Assigning)]
    [InlineData(OrderStatuses.Assigning, OrderStatuses.Assigned)]
    [InlineData(OrderStatuses.Assigned, OrderStatuses.InProgress)]
    [InlineData(OrderStatuses.InProgress, OrderStatuses.QaReview)]
    [InlineData(OrderStatuses.QaReview, OrderStatuses.CustomerReview)]
    [InlineData(OrderStatuses.CustomerReview, OrderStatuses.Completed)]
    // The branches that make the product work.
    [InlineData(OrderStatuses.Submitted, OrderStatuses.LocationReview)]
    [InlineData(OrderStatuses.LocationReview, OrderStatuses.EstimateReady)]
    [InlineData(OrderStatuses.EstimateReady, OrderStatuses.EstimateReady)]
    [InlineData(OrderStatuses.InProgress, OrderStatuses.ExtraApproval)]
    [InlineData(OrderStatuses.ExtraApproval, OrderStatuses.InProgress)]
    [InlineData(OrderStatuses.QaReview, OrderStatuses.InProgress)]
    [InlineData(OrderStatuses.Assigned, OrderStatuses.Assigning)]
    [InlineData(OrderStatuses.Completed, OrderStatuses.Disputed)]
    [InlineData(OrderStatuses.Disputed, OrderStatuses.Refunded)]
    public void Allowed_transitions_pass(string from, string to)
    {
        Assert.True(OrderStateMachine.CanTransition(from, to), $"{from} → {to} должен быть разрешён");
    }

    [Theory]
    // Money cannot be skipped. Each of these would mean work starting on an unpaid order.
    [InlineData(OrderStatuses.EstimateReady, OrderStatuses.Paid)]
    [InlineData(OrderStatuses.AwaitingPayment, OrderStatuses.Assigning)]
    [InlineData(OrderStatuses.Submitted, OrderStatuses.InProgress)]
    [InlineData(OrderStatuses.Draft, OrderStatuses.Paid)]
    // QA cannot be skipped: a report reaches the client only after approval (BR-010).
    [InlineData(OrderStatuses.InProgress, OrderStatuses.CustomerReview)]
    [InlineData(OrderStatuses.InProgress, OrderStatuses.Completed)]
    [InlineData(OrderStatuses.QaReview, OrderStatuses.Completed)]
    // A visit cannot start before an executor accepted the assignment (BR-007).
    [InlineData(OrderStatuses.Paid, OrderStatuses.InProgress)]
    [InlineData(OrderStatuses.Assigning, OrderStatuses.InProgress)]
    // Nothing comes back from a terminal state.
    [InlineData(OrderStatuses.Cancelled, OrderStatuses.Draft)]
    [InlineData(OrderStatuses.Cancelled, OrderStatuses.Paid)]
    [InlineData(OrderStatuses.Refunded, OrderStatuses.Completed)]
    // No going backwards to re-quote something already paid for; that is a dispute or a refund.
    [InlineData(OrderStatuses.Paid, OrderStatuses.EstimateReady)]
    [InlineData(OrderStatuses.Completed, OrderStatuses.InProgress)]
    public void Forbidden_transitions_are_refused(string from, string to)
    {
        Assert.False(OrderStateMachine.CanTransition(from, to), $"{from} → {to} должен быть запрещён");
    }

    [Fact]
    public void A_paid_order_cannot_be_cancelled_without_going_through_a_refund()
    {
        // Cancelling is allowed while nothing has been taken...
        Assert.True(OrderStateMachine.CanTransition(OrderStatuses.Draft, OrderStatuses.Cancelled));
        Assert.True(OrderStateMachine.CanTransition(OrderStatuses.EstimateReady, OrderStatuses.Cancelled));

        // ...and these stages know money is in, which is what stops the client-facing cancel path.
        Assert.True(OrderStateMachine.IsPaidStage(OrderStatuses.Paid));
        Assert.True(OrderStateMachine.IsPaidStage(OrderStatuses.InProgress));
        Assert.True(OrderStateMachine.IsPaidStage(OrderStatuses.Completed));

        Assert.False(OrderStateMachine.IsPaidStage(OrderStatuses.Draft));
        Assert.False(OrderStateMachine.IsPaidStage(OrderStatuses.AwaitingPayment));
    }

    [Fact]
    public void Terminal_states_have_no_way_out()
    {
        foreach (var terminal in new[] { OrderStatuses.Cancelled, OrderStatuses.Refunded })
        {
            Assert.Empty(OrderStateMachine.NextFrom(terminal));
        }

        // Completed is deliberately not empty: the warranty window still allows a complaint.
        Assert.Contains(OrderStatuses.Disputed, OrderStateMachine.NextFrom(OrderStatuses.Completed));
    }

    [Fact]
    public void Every_status_is_reachable_from_draft()
    {
        // A status nothing can reach is either dead code or a missing transition, and both are
        // worth failing a build over.
        var reachable = new HashSet<string> { OrderStatuses.Draft };
        var queue = new Queue<string>([OrderStatuses.Draft]);

        while (queue.Count > 0)
        {
            foreach (var next in OrderStateMachine.NextFrom(queue.Dequeue()))
            {
                if (reachable.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var unreachable = OrderStatuses.All.Except(reachable).ToList();
        Assert.True(unreachable.Count == 0, $"недостижимые статусы: {string.Join(", ", unreachable)}");
    }

    [Fact]
    public void Every_transition_target_is_a_known_status()
    {
        foreach (var status in OrderStatuses.All)
        {
            foreach (var target in OrderStateMachine.NextFrom(status))
            {
                Assert.Contains(target, OrderStatuses.All);
            }
        }
    }

    [Fact]
    public void Every_status_has_client_facing_text()
    {
        // An internal status leaking into the interface reads as a bug to the person who sees it.
        foreach (var status in OrderStatuses.All)
        {
            var state = OrderStatusPresentation.For(status);
            Assert.False(string.IsNullOrWhiteSpace(state.Label), $"нет текста для статуса {status}");
            Assert.DoesNotContain("_", state.Label);
        }
    }

    [Fact]
    public void The_client_may_only_edit_an_order_before_it_is_priced()
    {
        Assert.True(OrderStateMachine.IsClientEditable(OrderStatuses.Draft));
        Assert.True(OrderStateMachine.IsClientEditable(OrderStatuses.LocationReview));

        // Once an estimate is out, a change belongs in a new version rather than in the order.
        Assert.False(OrderStateMachine.IsClientEditable(OrderStatuses.EstimateReady));
        Assert.False(OrderStateMachine.IsClientEditable(OrderStatuses.Paid));
    }
}
