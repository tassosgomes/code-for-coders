using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Api.Clients;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class BillingClientTests : IDisposable
{
    private readonly RSA rsa = RSA.Create(2048);
    private readonly string keyId = "test-billing-key";
    private readonly BillingAssertionTokenFactory tokenFactory;

    public BillingClientTests()
    {
        var options = Options.Create(new BillingClientOptions
        {
            BaseUrl = "https://billing.local",
            Issuer = "commerce",
            Audience = "billing",
            SigningKeyId = keyId,
            SigningKeyBase64 = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())
        });
        tokenFactory = new BillingAssertionTokenFactory(options, TimeProvider.System);
    }

    [Fact(DisplayName = nameof(EnsureAsync_BuildsPutRequest_WithAssertionAndPayload))]
    public async Task EnsureAsync_BuildsPutRequest_WithAssertionAndPayload()
    {
        var tenantId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new TestDelegateHandler(async (req, ct) =>
        {
            capturedRequest = req;
            capturedBody = req.Content != null ? await req.Content.ReadAsStringAsync(ct) : null;
            var responsePayload = new
            {
                orderId,
                kind = "checkout",
                paymentUrl = "https://checkout.stripe.com/c/pay/cs_test_mock",
                expiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responsePayload)
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://billing.local/") };
        var client = new BillingPaymentClient(httpClient, tokenFactory);

        var request = new BillingPaymentRequest(
            tenantId,
            orderId,
            studentId,
            49700,
            "BRL",
            ".NET do zero à API — Acesso por 12 meses",
            "https://app.local/pedidos/123?res=ok",
            "https://app.local/pedidos/123?res=cancel");

        var result = await client.EnsureAsync(request, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Put, capturedRequest.Method);
        Assert.Equal($"https://billing.local/internal/v1/payment-sessions/{orderId:D}", capturedRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);

        // Verify JWT claims
        var token = capturedRequest.Headers.Authorization!.Parameter!;
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
        var claimsJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var claimsDoc = JsonDocument.Parse(claimsJson);
        var root = claimsDoc.RootElement;
        Assert.Equal("commerce", root.GetProperty("iss").GetString());
        Assert.Equal("billing", root.GetProperty("aud").GetString());
        Assert.Equal("payment:request", root.GetProperty("scope").GetString());
        Assert.Equal(tenantId, root.GetProperty("tenantId").GetGuid());

        // Verify request payload
        Assert.NotNull(capturedBody);
        using var bodyDoc = JsonDocument.Parse(capturedBody);
        var body = bodyDoc.RootElement;
        Assert.Equal(studentId, body.GetProperty("studentId").GetGuid());
        Assert.Equal(49700, body.GetProperty("amountCents").GetInt64());
        Assert.Equal("BRL", body.GetProperty("currency").GetString());
        Assert.Equal(".NET do zero à API — Acesso por 12 meses", body.GetProperty("description").GetString());
        Assert.Equal("https://app.local/pedidos/123?res=ok", body.GetProperty("successUrl").GetString());
        Assert.Equal("https://app.local/pedidos/123?res=cancel", body.GetProperty("cancelUrl").GetString());

        // Verify result
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("checkout", result.Kind);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_mock", result.PaymentUrl);
    }

    [Fact(DisplayName = nameof(EnsureAsync_ParsesSuccessful200Response))]
    public async Task EnsureAsync_ParsesSuccessful200Response()
    {
        var tenantId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

        var handler = new TestDelegateHandler((_, _) =>
        {
            var responsePayload = new
            {
                orderId,
                kind = "checkout",
                paymentUrl = "https://checkout.stripe.com/c/pay/cs_test_success",
                expiresAt
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responsePayload)
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://billing.local/") };
        var client = new BillingPaymentClient(httpClient, tokenFactory);

        var request = new BillingPaymentRequest(
            tenantId,
            orderId,
            Guid.CreateVersion7(),
            49700,
            "BRL",
            "Curso",
            "https://app.local/ok",
            "https://app.local/cancel");

        var result = await client.EnsureAsync(request, CancellationToken.None);

        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("checkout", result.Kind);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_success", result.PaymentUrl);
        Assert.Null(result.Method);
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), result.ExpiresAt.ToUnixTimeSeconds());
    }

    [Theory(DisplayName = nameof(EnsureAsync_MapsFailuresToPaymentProviderUnavailableException))]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{\"code\":\"GATEWAY_UNAVAILABLE\"}")]
    [InlineData(HttpStatusCode.InternalServerError, "{\"code\":\"UNEXPECTED_ERROR\"}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":\"INVALID_REQUEST\"}")]
    [InlineData(HttpStatusCode.OK, "not a valid json")]
    [InlineData(HttpStatusCode.OK, "{\"orderId\":\"00000000-0000-0000-0000-000000000000\",\"kind\":\"checkout\",\"paymentUrl\":\"https://foo.com\"}")]
    [InlineData(HttpStatusCode.OK, "{\"orderId\":\"__MATCH__\",\"kind\":\"other\",\"paymentUrl\":\"https://foo.com\",\"expiresAt\":\"2026-10-05T20:00:00Z\"}")]
    [InlineData(HttpStatusCode.OK, "{\"orderId\":\"__MATCH__\",\"kind\":\"checkout\",\"paymentUrl\":\"ftp://foo.com\",\"expiresAt\":\"2026-10-05T20:00:00Z\"}")]
    public async Task EnsureAsync_MapsFailuresToPaymentProviderUnavailableException(HttpStatusCode status, string rawBody)
    {
        var tenantId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();

        var bodyToSend = rawBody.Replace("__MATCH__", orderId.ToString("D"));

        var handler = new TestDelegateHandler((_, _) =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(bodyToSend, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://billing.local/") };
        var client = new BillingPaymentClient(httpClient, tokenFactory);

        var request = new BillingPaymentRequest(
            tenantId,
            orderId,
            Guid.CreateVersion7(),
            49700,
            "BRL",
            "Curso",
            "https://app.local/ok",
            "https://app.local/cancel");

        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() =>
            client.EnsureAsync(request, CancellationToken.None));
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }

    public void Dispose()
    {
        rsa.Dispose();
    }

    private sealed class TestDelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
