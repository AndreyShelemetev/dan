using System.Collections.Concurrent;
using PamyatRyadom.Api.Models.Payments;

namespace PamyatRyadom.Api.Services.Payments;

/// <summary>
/// A payment provider that takes no money.
///
/// Stands in until the YooKassa adapter is written, so the rest of the product — the order moving
/// to paid, dispatch picking it up, a refund reversing it — can be built and tested against a
/// real interface instead of against nothing.
///
/// It is registered only outside Production, and refuses to construct there. That is deliberate
/// and mirrors the email sender: the environment decides, so no configuration value can put a
/// provider that invents successful payments in front of real customers.
///
/// It confirms nothing on its own. A stub that auto-succeeds would hide every bug in the code
/// that waits for confirmation, which is precisely the code that matters here — so a payment sits
/// in <see cref="PaymentStatuses.WaitingForCapture"/> until something explicitly confirms it,
/// exactly as it would while a client is on a real provider's page.
/// </summary>
public sealed class StubPaymentProvider : IPaymentProvider
{
    private readonly ConcurrentDictionary<string, ProviderPayment> _payments = new();
    private readonly ConcurrentDictionary<string, string> _byIdempotenceKey = new();
    private readonly ILogger<StubPaymentProvider> _logger;

    public StubPaymentProvider(IHostEnvironment environment, ILogger<StubPaymentProvider> logger)
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "StubPaymentProvider must never run in Production — it reports payments that never happened.");
        }

        _logger = logger;
    }

    public string Name => "stub";

    public Task<ProviderPayment> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        // Same key, same payment: the property the real provider guarantees and the one every
        // retry path depends on. A stub that ignored it would let retries look safe here and
        // double-charge in production.
        if (_byIdempotenceKey.TryGetValue(request.IdempotenceKey, out var existingId))
        {
            return Task.FromResult(_payments[existingId]);
        }

        var id = $"stub-{Guid.NewGuid():N}";
        var payment = new ProviderPayment(
            Id: id,
            Status: PaymentStatuses.WaitingForCapture,
            AmountRub: request.AmountRub,
            RefundedRub: 0m,
            // Points back at us, not at a provider: the client confirms on our own stub page.
            ConfirmationUrl: $"{request.ReturnUrl}?stub_payment={id}",
            FailureReason: null);

        _payments[id] = payment;
        _byIdempotenceKey[request.IdempotenceKey] = id;

        _logger.LogInformation("Stub payment {PaymentId} created for {OrderRef}", id, request.OrderRef);
        return Task.FromResult(payment);
    }

    public Task<ProviderPayment> GetAsync(string providerPaymentId, CancellationToken ct = default) =>
        _payments.TryGetValue(providerPaymentId, out var payment)
            ? Task.FromResult(payment)
            : throw new KeyNotFoundException($"No stub payment '{providerPaymentId}'.");

    public Task<ProviderRefund> RefundAsync(
        string providerPaymentId, decimal amountRub, string idempotenceKey, CancellationToken ct = default)
    {
        if (!_payments.TryGetValue(providerPaymentId, out var payment))
        {
            throw new KeyNotFoundException($"No stub payment '{providerPaymentId}'.");
        }

        if (payment.Status != PaymentStatuses.Succeeded)
        {
            throw new InvalidOperationException("Only a succeeded payment can be refunded.");
        }

        var refunded = payment.RefundedRub + amountRub;
        if (refunded > payment.AmountRub)
        {
            throw new InvalidOperationException("Refund exceeds the amount paid.");
        }

        _payments[providerPaymentId] = payment with
        {
            RefundedRub = refunded,
            Status = refunded >= payment.AmountRub ? PaymentStatuses.Refunded : payment.Status,
        };

        return Task.FromResult(new ProviderRefund($"stub-refund-{Guid.NewGuid():N}", "succeeded", amountRub));
    }

    /// <summary>Development-only: stands in for the client finishing on the provider's page.
    /// Nothing in the request pipeline may call this — it exists for the dev confirm endpoint and
    /// for tests, which need a way to make a payment succeed without a real provider.</summary>
    public void ConfirmForTesting(string providerPaymentId)
    {
        if (!_payments.TryGetValue(providerPaymentId, out var payment))
        {
            throw new KeyNotFoundException($"No stub payment '{providerPaymentId}'.");
        }

        _payments[providerPaymentId] = payment with { Status = PaymentStatuses.Succeeded };
    }
}
