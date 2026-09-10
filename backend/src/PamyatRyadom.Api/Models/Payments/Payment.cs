namespace PamyatRyadom.Api.Models.Payments;

/// <summary>
/// One attempt to take money for one order.
///
/// A row per attempt, not per order: a client who abandons a checkout and comes back has two
/// attempts and one order, and collapsing them loses the ability to say which reference the
/// provider actually settled.
///
/// Nothing here is ever written from a webhook body. A callback only says "look again"; the
/// status recorded is always the one re-fetched from the provider (BR-007). That is what stops a
/// forged callback from marking an order paid.
/// </summary>
public sealed class Payment
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    /// <summary>The reference we generate and the provider echoes back. Unique, and the key that
    /// makes retries idempotent: the same reference must never produce a second charge.</summary>
    public string OrderRef { get; set; } = string.Empty;

    /// <summary>Which integration created this — "yookassa", or "stub" in development. Stored so
    /// a row can never be reconciled against the wrong provider's API.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The provider's own id for the payment. Null until they have accepted it.</summary>
    public string? ProviderPaymentId { get; set; }

    /// <summary>The key sent with the create call so a retried request is not a second payment.
    /// Generated once and reused for every retry of the same attempt.</summary>
    public string IdempotenceKey { get; set; } = string.Empty;

    public string Status { get; set; } = PaymentStatuses.Pending;

    /// <summary>What we asked for, in roubles. Frozen at creation: the estimate it came from can
    /// be superseded later, and the amount actually charged must not follow it.</summary>
    public decimal AmountRub { get; set; }

    /// <summary>Which estimate version this pays for. An acceptance is version-specific, so a
    /// payment has to name the version it settles or it settles nothing in particular.</summary>
    public int EstimateVersion { get; set; }

    /// <summary>Where the client is sent to pay. Short-lived and provider-owned.</summary>
    public string? ConfirmationUrl { get; set; }

    public decimal RefundedRub { get; set; }

    /// <summary>Why the provider refused, in their words. Kept for support, shown to nobody
    /// verbatim — a provider's error strings are not client-facing copy.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }

    /// <summary>When we last asked the provider what it thinks. A payment nobody has re-checked
    /// is a payment whose status is a guess.</summary>
    public DateTimeOffset? LastCheckedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Orders.Order? Order { get; set; }
}

public static class PaymentStatuses
{
    /// <summary>Created on our side; the client has not been sent anywhere yet.</summary>
    public const string Pending = "pending";

    /// <summary>The provider has it and is waiting for the client. This is where a payment sits
    /// while the client is on the provider's page.</summary>
    public const string WaitingForCapture = "waiting_for_capture";

    public const string Succeeded = "succeeded";
    public const string Canceled = "canceled";
    public const string Failed = "failed";

    /// <summary>Money returned in full. A partial return stays <see cref="Succeeded"/> with a
    /// non-zero <see cref="Payment.RefundedRub"/> — "partly refunded" is not a payment state, it
    /// is an amount.</summary>
    public const string Refunded = "refunded";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Pending, WaitingForCapture, Succeeded, Canceled, Failed, Refunded,
    };

    /// <summary>States nothing moves out of. Re-checking one with the provider is wasted work.</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[]
    {
        Succeeded, Canceled, Failed, Refunded,
    };

    public static bool IsTerminal(string status) => Terminal.Contains(status);
}
