namespace PamyatRyadom.Api.Services.Payments;

/// <summary>
/// The payment provider, as the rest of the application needs to see it.
///
/// Three operations, and deliberately no fourth: there is no "mark as paid". A payment becomes
/// paid because <see cref="GetAsync"/> said so, never because a callback claimed it — that is
/// the whole reason this interface has a read method at all.
///
/// Refunds are part of the contract rather than a later addition. An integration that can take
/// money and cannot give it back is not finished, and making the refund path optional is how it
/// stays unwritten.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Which integration this is — recorded on every payment row so a payment is never
    /// reconciled against a different provider's API than the one that created it.</summary>
    string Name { get; }

    /// <summary>Registers a payment and returns where to send the client.
    ///
    /// Must be idempotent on <see cref="CreatePaymentRequest.IdempotenceKey"/>: retrying the same
    /// key returns the existing payment, and never charges twice.</summary>
    Task<ProviderPayment> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default);

    /// <summary>The provider's current view of a payment. The only source of truth for status.</summary>
    Task<ProviderPayment> GetAsync(string providerPaymentId, CancellationToken ct = default);

    /// <summary>Returns money. <paramref name="amountRub"/> may be less than the payment for a
    /// partial refund.</summary>
    Task<ProviderRefund> RefundAsync(
        string providerPaymentId, decimal amountRub, string idempotenceKey, CancellationToken ct = default);
}

/// <param name="OrderRef">Our reference, echoed back by the provider.</param>
/// <param name="AmountRub">Charge amount. Decimal roubles — never a float, never minor units.</param>
/// <param name="Description">What the client sees on the provider's page and their statement.
/// Carries no personal data: an order number, not a name or a cemetery.</param>
/// <param name="ReturnUrl">Where the provider sends the browser afterwards. Arriving here proves
/// only that the client came back, never that they paid.</param>
public sealed record CreatePaymentRequest(
    string OrderRef,
    decimal AmountRub,
    string Description,
    string ReturnUrl,
    string IdempotenceKey);

/// <param name="Status">One of <see cref="Models.Payments.PaymentStatuses"/>, already mapped out
/// of the provider's vocabulary by the adapter.</param>
public sealed record ProviderPayment(
    string Id,
    string Status,
    decimal AmountRub,
    decimal RefundedRub,
    string? ConfirmationUrl,
    string? FailureReason);

public sealed record ProviderRefund(string Id, string Status, decimal AmountRub);
