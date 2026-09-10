using PamyatRyadom.Api.Models.Orders;

namespace PamyatRyadom.Api.Tests.Orders;

/// <summary>
/// Every status lands in exactly one pile on the staff queue.
///
/// The queue page renders three groups and drops anything it does not recognise, so a status
/// added later without a group would silently vanish from the dispatcher's list — which is the
/// one failure mode a queue must not have.
/// </summary>
public sealed class QueueGroupingTests
{
    [Fact]
    public void Every_status_belongs_to_exactly_one_group()
    {
        foreach (var status in OrderStatuses.All)
        {
            var memberships = new[]
            {
                OrderStatuses.NeedsStaffAction.Contains(status),
                OrderStatuses.WaitingOnCustomer.Contains(status),
                OrderStatuses.InFlight.Contains(status),
                OrderStatuses.Terminal.Contains(status),
                status == OrderStatuses.Draft,
            }.Count(inGroup => inGroup);

            Assert.True(memberships == 1, $"'{status}' is in {memberships} groups, expected exactly 1.");
        }
    }

    [Theory]
    [InlineData(OrderStatuses.Submitted, "staff")]
    [InlineData(OrderStatuses.LocationReview, "staff")]
    [InlineData(OrderStatuses.Disputed, "staff")]
    [InlineData(OrderStatuses.EstimateReady, "customer")]
    [InlineData(OrderStatuses.AwaitingPayment, "customer")]
    [InlineData(OrderStatuses.InProgress, "in_flight")]
    [InlineData(OrderStatuses.Completed, "done")]
    public void The_group_matches_who_the_order_is_actually_waiting_on(string status, string expected) =>
        Assert.Equal(expected, OrderStatuses.QueueGroup(status));
}
