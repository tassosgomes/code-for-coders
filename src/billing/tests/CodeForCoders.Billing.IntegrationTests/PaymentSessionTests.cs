using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CodeForCoders.Billing.IntegrationTests;

[Collection(BillingIntegrationCollection.Name)]
public sealed class PaymentSessionTests(BillingIntegrationFixture fixture) : IAsyncLifetime
{
    private readonly BillingTestApp app = new(fixture);
    private Guid TenantId => app.AllowedTenantId;
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await fixture.PurgeNamespaceAsync(app.ProcessingNamespace);
        app.Dispose();
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionReturnsCheckoutUrlAndIsIdempotentForSameOrder))]
    public async Task EnsurePaymentSessionReturnsCheckoutUrlAndIsIdempotentForSameOrder()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var expectedUrl = "https://checkout.stripe.com/c/pay/cs_test_session_1";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        app.GatewayMock.Setup(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySession("cs_test_session_1", expectedUrl, expiresAt));
        app.GatewayMock.Setup(g => g.GetAsync("cs_test_session_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySession("cs_test_session_1", expectedUrl, expiresAt));

        using var client = app.CreateClient();
        var token = app.CreateCommerceAssertion(TenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = ".NET do zero à API — Acesso por 12 meses",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        // First call
        using var firstResponse = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(orderId, firstBody.GetProperty("orderId").GetGuid());
        Assert.Equal("checkout", firstBody.GetProperty("kind").GetString());
        Assert.Equal(expectedUrl, firstBody.GetProperty("paymentUrl").GetString());

        // Second call (idempotent)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));
        using var secondResponse = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(expectedUrl, secondBody.GetProperty("paymentUrl").GetString());

        // Verify gateway OpenAsync was called only once
        app.GatewayMock.Verify(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        app.GatewayMock.Verify(g => g.GetAsync("cs_test_session_1", It.IsAny<CancellationToken>()), Times.Once);

        // Verify DB has only 1 payment row
        await using var db = await app.CreateDbContextAsync(TenantId);
        var count = await db.Payments.CountAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionRejectsDivergentTermsWith422PaymentTermsConflict))]
    public async Task EnsurePaymentSessionRejectsDivergentTermsWith422PaymentTermsConflict()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        app.GatewayMock.Setup(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySession("cs_test_session_2", "https://checkout.stripe.com/pay/2", expiresAt));

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        var initialPayload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = ".NET do zero à API",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var initialResponse = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", initialPayload, Cancellation);
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        // Divergent terms (different amount)
        var divergentPayload = new
        {
            studentId,
            amountCents = 59700,
            currency = "BRL",
            description = ".NET do zero à API",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));
        using var divergentResponse = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", divergentPayload, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, divergentResponse.StatusCode);
        var error = await divergentResponse.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PAYMENT_TERMS_CONFLICT", error.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionRejectsDisallowedReturnUrlWith400InvalidRequest))]
    public async Task EnsurePaymentSessionRejectsDisallowedReturnUrlWith400InvalidRequest()
    {
        var orderId = Guid.CreateVersion7();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        var payload = new
        {
            studentId = Guid.CreateVersion7(),
            amountCents = 49700,
            currency = "BRL",
            description = "Curso",
            successUrl = "https://malicious-site.com/return",
            cancelUrl = "https://app.code4coders.com.br/cancel"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("INVALID_REQUEST", error.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionAcceptsLocalhostHttpUrlInDevelopment))]
    public async Task EnsurePaymentSessionAcceptsLocalhostHttpUrlInDevelopment()
    {
        var orderId = Guid.CreateVersion7();
        app.GatewayMock.Setup(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySession("cs_test_local", "https://checkout.stripe.com/pay/local", DateTimeOffset.UtcNow.AddHours(24)));

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        var payload = new
        {
            studentId = Guid.CreateVersion7(),
            amountCents = 49700,
            currency = "BRL",
            description = "Curso",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionRejectsExpiredPaymentPageWith422PaymentExpired))]
    public async Task EnsurePaymentSessionRejectsExpiredPaymentPageWith422PaymentExpired()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();

        // Seed expired payment in database
        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Expired"));
            payment.Open("cs_expired", DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(-25));
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        var payload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = "Curso Expired",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PAYMENT_EXPIRED", error.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionReturns503WhenGatewayUnavailable))]
    public async Task EnsurePaymentSessionReturns503WhenGatewayUnavailable()
    {
        var orderId = Guid.CreateVersion7();
        app.GatewayMock.Setup(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GatewayUnavailableException());

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        var payload = new
        {
            studentId = Guid.CreateVersion7(),
            amountCents = 49700,
            currency = "BRL",
            description = "Curso",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("GATEWAY_UNAVAILABLE", error.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionRejectsInvalidAssertionOrScope))]
    public async Task EnsurePaymentSessionRejectsInvalidAssertionOrScope()
    {
        var orderId = Guid.CreateVersion7();
        using var client = app.CreateClient();

        var payload = new
        {
            studentId = Guid.CreateVersion7(),
            amountCents = 49700,
            currency = "BRL",
            description = "Curso",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        // Missing token -> 401
        using var noToken = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        var noTokenErr = await noToken.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("SERVICE_ASSERTION_INVALID", noTokenErr.GetProperty("code").GetString());

        // Wrong scope -> 403
        using var wrongScopeClient = app.CreateClient();
        wrongScopeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId, scope: "wrong:scope"));
        using var forbidden = await wrongScopeClient.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var forbiddenErr = await forbidden.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("SCOPE_DENIED", forbiddenErr.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionEnforcesDescriptionLimit263))]
    public async Task EnsurePaymentSessionEnforcesDescriptionLimit263()
    {
        var orderId263 = Guid.CreateVersion7();
        var orderId264 = Guid.CreateVersion7();
        app.GatewayMock.Setup(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySession("cs_test_desc", "https://checkout.stripe.com/pay/desc", DateTimeOffset.UtcNow.AddHours(24)));

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        // Exactly 263 characters -> Accepted (200)
        var desc263 = new string('A', 263);
        var payload263 = new
        {
            studentId = Guid.CreateVersion7(),
            amountCents = 49700,
            currency = "BRL",
            description = desc263,
            successUrl = $"http://localhost:8082/student/pedidos/{orderId263:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId263:D}?resultado=saiu"
        };
        using var response263 = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId263:D}", payload263, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response263.StatusCode);

        // 264 characters -> Rejected (400)
        var desc264 = new string('A', 264);
        var payload264 = new
        {
            studentId = Guid.CreateVersion7(),
            amountCents = 49700,
            currency = "BRL",
            description = desc264,
            successUrl = $"http://localhost:8082/student/pedidos/{orderId264:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId264:D}?resultado=saiu"
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));
        using var response264 = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId264:D}", payload264, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response264.StatusCode);
        var error = await response264.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("INVALID_REQUEST", error.GetProperty("code").GetString());
    }
}
