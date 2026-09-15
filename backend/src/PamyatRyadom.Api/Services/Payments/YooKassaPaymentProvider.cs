using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Models.Payments;

namespace PamyatRyadom.Api.Services.Payments;

/// <summary>
/// The real thing: YooKassa's redirect-confirmation API (api.yookassa.ru/v3).
///
/// Every call is Basic-authenticated with the shop id and secret key, and every write carries an
/// <c>Idempotence-Key</c> — the same key on a retry returns the original result rather than a
/// second charge or a second refund. Nothing here decides that a payment is paid: <see
/// cref="GetAsync"/> reports whatever YooKassa's own status says, unmapped meanings are refused
/// rather than guessed at, and there is no path from a request or a response body straight to
/// <see cref="PaymentStatuses.Succeeded"/>.
/// </summary>
public sealed class YooKassaPaymentProvider : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<YooKassaPaymentProvider> _logger;

    public YooKassaPaymentProvider(
        HttpClient http, IOptions<YooKassaOptions> options, ILogger<YooKassaPaymentProvider> logger)
    {
        var configured = options.Value;

        // A build with no way to take money should fail at startup, not the first time a client
        // tries to pay.
        if (string.IsNullOrWhiteSpace(configured.ShopId) || string.IsNullOrWhiteSpace(configured.SecretKey))
        {
            throw new InvalidOperationException(
                "YooKassa is not configured: YOOKASSA_SHOP_ID and YOOKASSA_SECRET_KEY are required.");
        }

        http.BaseAddress ??= new Uri(configured.ApiBaseUrl);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{configured.ShopId}:{configured.SecretKey}")));

        _http = http;
        _logger = logger;
    }

    public string Name => "yookassa";

    public async Task<ProviderPayment> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(
                new WirePaymentRequest(
                    new WireAmount(FormatAmount(request.AmountRub), "RUB"),
                    new WireConfirmationRequest("redirect", request.ReturnUrl),
                    Capture: true,
                    request.Description),
                options: JsonOptions),
        };
        httpRequest.Headers.Add("Idempotence-Key", request.IdempotenceKey);

        var payment = await SendAsync<WirePayment>(httpRequest, ct);
        return Map(payment);
    }

    public async Task<ProviderPayment> GetAsync(string providerPaymentId, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"payments/{providerPaymentId}");
        var payment = await SendAsync<WirePayment>(httpRequest, ct);
        return Map(payment);
    }

    public async Task<ProviderRefund> RefundAsync(
        string providerPaymentId, decimal amountRub, string idempotenceKey, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "refunds")
        {
            Content = JsonContent.Create(
                new WireRefundRequest(providerPaymentId, new WireAmount(FormatAmount(amountRub), "RUB")),
                options: JsonOptions),
        };
        httpRequest.Headers.Add("Idempotence-Key", idempotenceKey);

        var refund = await SendAsync<WireRefund>(httpRequest, ct);
        return new ProviderRefund(refund.Id, refund.Status, ParseAmount(refund.Amount));
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage httpRequest, CancellationToken ct)
    {
        using var response = await _http.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // The body is YooKassa's own error description — logged for support, never shown to
            // a client (CLAUDE.md: PII and provider internals stay out of client-facing copy).
            _logger.LogError(
                "YooKassa {Method} {Path} returned {Status}", httpRequest.Method, httpRequest.RequestUri, response.StatusCode);
            throw new HttpRequestException($"YooKassa request failed with {(int)response.StatusCode}.");
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException("YooKassa returned an empty response body.");
    }

    private static ProviderPayment Map(WirePayment payment) => new(
        Id: payment.Id,
        Status: MapStatus(payment.Status),
        AmountRub: ParseAmount(payment.Amount),
        RefundedRub: payment.RefundedAmount is null ? 0m : ParseAmount(payment.RefundedAmount),
        ConfirmationUrl: payment.Confirmation?.ConfirmationUrl,
        FailureReason: payment.CancellationDetails?.Reason);

    /// <summary>YooKassa's four payment statuses, and only those — an unrecognised one is a sign
    /// the provider added something new, not a status this adapter gets to guess about.</summary>
    private static string MapStatus(string yooKassaStatus) => yooKassaStatus switch
    {
        "pending" => PaymentStatuses.Pending,
        "waiting_for_capture" => PaymentStatuses.WaitingForCapture,
        "succeeded" => PaymentStatuses.Succeeded,
        "canceled" => PaymentStatuses.Canceled,
        _ => throw new InvalidOperationException($"Unrecognised YooKassa payment status '{yooKassaStatus}'."),
    };

    private static string FormatAmount(decimal amountRub) => amountRub.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(WireAmount amount) =>
        decimal.Parse(amount.Value, NumberStyles.Number, CultureInfo.InvariantCulture);

    // The wire shapes below are YooKassa's vocabulary, not ours — kept private so nothing outside
    // this file can come to depend on their field names instead of on IPaymentProvider.

    private sealed record WireAmount(string Value, string Currency);

    private sealed record WireConfirmationRequest(string Type, [property: JsonPropertyName("return_url")] string ReturnUrl);

    private sealed record WirePaymentRequest(
        WireAmount Amount, WireConfirmationRequest Confirmation, bool Capture, string Description);

    private sealed record WireConfirmationResponse(
        [property: JsonPropertyName("confirmation_url")] string? ConfirmationUrl);

    private sealed record WireCancellationDetails(string? Party, string? Reason);

    private sealed record WirePayment(
        string Id,
        string Status,
        WireAmount Amount,
        [property: JsonPropertyName("refunded_amount")] WireAmount? RefundedAmount,
        WireConfirmationResponse? Confirmation,
        [property: JsonPropertyName("cancellation_details")] WireCancellationDetails? CancellationDetails);

    private sealed record WireRefundRequest(
        [property: JsonPropertyName("payment_id")] string PaymentId, WireAmount Amount);

    private sealed record WireRefund(string Id, string Status, WireAmount Amount);
}
