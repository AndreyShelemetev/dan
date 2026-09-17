using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Models.Payments;
using PamyatRyadom.Api.Services.Payments;

namespace PamyatRyadom.Api.Tests.Payments;

/// <summary>
/// The YooKassa adapter, against a fake handler — no network, no secrets, no live shop.
///
/// What matters here is the two directions of translation: our request becomes YooKassa's wire
/// shape with the right authentication and idempotence key, and YooKassa's wire shape becomes our
/// three-status vocabulary rather than something invented in between.
/// </summary>
public sealed class YooKassaPaymentProviderTests
{
    private const string ShopId = "shop-42";
    private const string SecretKey = "test-secret-key";

    [Fact]
    public async Task CreateAsync_sends_basic_auth_idempotence_key_and_the_right_body()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            {
              "id": "22e12f66-000f-5000-8000-18db351245c7",
              "status": "pending",
              "amount": { "value": "1500.00", "currency": "RUB" },
              "confirmation": { "type": "redirect", "confirmation_url": "https://yookassa.ru/checkout/pay/abc" }
            }
            """);

        var provider = CreateProvider(handler);

        var result = await provider.CreateAsync(new CreatePaymentRequest(
            OrderRef: "PR-1-v1-abc",
            AmountRub: 1500m,
            Description: "Заказ PR-1",
            ReturnUrl: "https://pamyat-ryadom.ru/cabinet/orders/1",
            IdempotenceKey: "idem-key-1"));

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/v3/payments", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("idem-key-1", handler.LastRequest.Headers.GetValues("Idempotence-Key").Single());

        var auth = handler.LastRequest.Headers.Authorization;
        Assert.Equal("Basic", auth!.Scheme);
        Assert.Equal(
            $"{ShopId}:{SecretKey}",
            Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter!)));

        Assert.Contains("\"value\":\"1500.00\"", handler.LastRequestBody);
        Assert.Contains("\"currency\":\"RUB\"", handler.LastRequestBody);
        Assert.Contains("\"type\":\"redirect\"", handler.LastRequestBody);
        Assert.Contains("\"return_url\":\"https://pamyat-ryadom.ru/cabinet/orders/1\"", handler.LastRequestBody);
        Assert.Contains("\"capture\":true", handler.LastRequestBody);

        // Parsed rather than matched as a raw substring: the default encoder escapes Cyrillic to
        // \uXXXX, which is correct JSON but would never match a literal Cyrillic Contains().
        using var sentBody = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("Заказ PR-1", sentBody.RootElement.GetProperty("description").GetString());
        // Never the deceased's name or any other personal data — only the order reference.
        Assert.DoesNotContain("deceasedFullName", handler.LastRequestBody);

        Assert.Equal("22e12f66-000f-5000-8000-18db351245c7", result.Id);
        Assert.Equal(PaymentStatuses.Pending, result.Status);
        Assert.Equal(1500m, result.AmountRub);
        Assert.Equal("https://yookassa.ru/checkout/pay/abc", result.ConfirmationUrl);
    }

    [Theory]
    [InlineData("pending", PaymentStatuses.Pending)]
    [InlineData("waiting_for_capture", PaymentStatuses.WaitingForCapture)]
    [InlineData("succeeded", PaymentStatuses.Succeeded)]
    [InlineData("canceled", PaymentStatuses.Canceled)]
    public async Task GetAsync_maps_every_yookassa_status(string wireStatus, string expected)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, $$"""
            {
              "id": "pay-1",
              "status": "{{wireStatus}}",
              "amount": { "value": "500.00", "currency": "RUB" }
            }
            """);

        var result = await CreateProvider(handler).GetAsync("pay-1");

        Assert.Equal(expected, result.Status);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/v3/payments/pay-1", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAsync_refuses_to_guess_at_an_unrecognised_status()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            { "id": "pay-1", "status": "some_future_status", "amount": { "value": "500.00", "currency": "RUB" } }
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProvider(handler).GetAsync("pay-1"));
    }

    [Fact]
    public async Task GetAsync_carries_the_refund_total_and_the_cancellation_reason()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            {
              "id": "pay-1",
              "status": "canceled",
              "amount": { "value": "1000.00", "currency": "RUB" },
              "refunded_amount": { "value": "400.00", "currency": "RUB" },
              "cancellation_details": { "party": "yoo_money", "reason": "general_decline" }
            }
            """);

        var result = await CreateProvider(handler).GetAsync("pay-1");

        Assert.Equal(400m, result.RefundedRub);
        Assert.Equal("general_decline", result.FailureReason);
    }

    [Fact]
    public async Task RefundAsync_sends_the_payment_id_amount_and_idempotence_key()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            { "id": "refund-1", "status": "succeeded", "amount": { "value": "300.00", "currency": "RUB" } }
            """);

        var result = await CreateProvider(handler).RefundAsync("pay-1", 300m, "refund-idem-1");

        Assert.Equal("/v3/refunds", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("refund-idem-1", handler.LastRequest.Headers.GetValues("Idempotence-Key").Single());
        Assert.Contains("\"payment_id\":\"pay-1\"", handler.LastRequestBody);
        Assert.Contains("\"value\":\"300.00\"", handler.LastRequestBody);

        Assert.Equal("refund-1", result.Id);
        Assert.Equal("succeeded", result.Status);
        Assert.Equal(300m, result.AmountRub);
    }

    [Fact]
    public async Task A_non_success_response_throws_without_leaking_the_provider_body()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest, """
            { "type": "error", "id": "err-1", "code": "invalid_request", "description": "shop credentials rejected" }
            """);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateProvider(handler).GetAsync("pay-1"));
        Assert.DoesNotContain("shop credentials rejected", ex.Message);
    }

    [Fact]
    public void Refuses_to_construct_without_shop_credentials()
    {
        Assert.Throws<InvalidOperationException>(() => new YooKassaPaymentProvider(
            new HttpClient(new FakeHandler(HttpStatusCode.OK, "{}")),
            Options.Create(new YooKassaOptions { ShopId = "", SecretKey = "" }),
            NullLogger<YooKassaPaymentProvider>.Instance));
    }

    private static YooKassaPaymentProvider CreateProvider(FakeHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new YooKassaOptions { ShopId = ShopId, SecretKey = SecretKey }),
            NullLogger<YooKassaPaymentProvider>.Instance);

    /// <summary>Returns one canned response and records the last request — enough to test an
    /// HTTP adapter without a socket, a container or a real shop.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;

        public FakeHandler(HttpStatusCode status, string responseBody)
        {
            _status = status;
            _responseBody = responseBody;
        }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
