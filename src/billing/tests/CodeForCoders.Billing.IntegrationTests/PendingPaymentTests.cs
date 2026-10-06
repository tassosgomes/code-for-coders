using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CodeForCoders.Billing.IntegrationTests;

[Collection(BillingIntegrationCollection.Name)]
public sealed class PendingPaymentTests(BillingIntegrationFixture fixture) : IAsyncLifetime
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

    [Fact(DisplayName = nameof(CheckoutSessionCompletedUnpaidPixMarksAwaitingAndProducesOutboxFact))]
    public async Task CheckoutSessionCompletedUnpaidPixMarksAwaitingAndProducesOutboxFact()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_pix_flow_1";
        var paymentRef = "pi_pix_flow_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso PIX"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync(paymentRef, It.IsAny<CancellationToken>())).ReturnsAsync("pix");

        using var client = app.CreateClient();
        var eventId = $"evt_pix_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.completed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = "unpaid",
                    payment_intent = paymentRef,
                    payment_method_types = new[] { "card", "pix", "boleto" },
                    amount_total = 49700,
                    currency = "brl",
                    metadata = new
                    {
                        tenantId = TenantId.ToString("D"),
                        orderId = orderId.ToString("D")
                    }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        var processedCount = await store.ProcessPendingAsync(Cancellation);
        Assert.True(processedCount >= 1);

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var awaitingPayment = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("awaiting", awaitingPayment.Status);
        Assert.Equal("pix", awaitingPayment.Method);
        var pixFact = JsonDocument.Parse((await dbCheck.OutboxMessages.SingleAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation)).Payload).RootElement;
        Assert.Equal("pix", pixFact.GetProperty("method").GetString());
        Assert.Equal(paymentRef, awaitingPayment.GatewayReference);
        Assert.InRange(awaitingPayment.ExpiresAt, DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow.AddHours(25));

        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        Assert.Equal("PagamentoAguardando", outboxMessage.Type);

        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(CheckoutSessionCompletedUnpaidBoletoMarksAwaitingAndProducesOutboxFact))]
    public async Task CheckoutSessionCompletedUnpaidBoletoMarksAwaitingAndProducesOutboxFact()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_bol_flow_1";
        var paymentRef = "pi_bol_flow_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Boleto"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync(paymentRef, It.IsAny<CancellationToken>())).ReturnsAsync("boleto");

        using var client = app.CreateClient();
        var eventId = $"evt_bol_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.completed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = "unpaid",
                    payment_intent = paymentRef,
                    payment_method_types = new[] { "card", "pix", "boleto" },
                    amount_total = 49700,
                    currency = "brl",
                    metadata = new
                    {
                        tenantId = TenantId.ToString("D"),
                        orderId = orderId.ToString("D")
                    }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        var processedCount = await store.ProcessPendingAsync(Cancellation);
        Assert.True(processedCount >= 1);

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var awaitingPayment = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("awaiting", awaitingPayment.Status);
        Assert.Equal("boleto", awaitingPayment.Method);
        Assert.Equal(paymentRef, awaitingPayment.GatewayReference);
        Assert.Equal(TimeSpan.FromHours(-3), awaitingPayment.ExpiresAt.ToOffset(TimeSpan.FromHours(-3)).Offset);
        Assert.Equal(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).Date.AddDays(3).AddHours(23).AddMinutes(59).AddSeconds(59),
         awaitingPayment.ExpiresAt.ToOffset(TimeSpan.FromHours(-3)).DateTime);

        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        Assert.Equal("PagamentoAguardando", outboxMessage.Type);

        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionForAwaitingPaymentReturnsInstructionsWithoutCreatingNewSession))]
    public async Task EnsurePaymentSessionForAwaitingPaymentReturnsInstructionsWithoutCreatingNewSession()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_awaiting_session_1";
        var paymentRef = "pi_awaiting_session_1";
        var instructionUrl = "https://pay.stripe.com/receipts/pix_instructions_test_url";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Instruções"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("pix", paymentRef, expiresAt);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock
            .Setup(g => g.GetInstructionsUrlAsync(paymentRef, "pix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instructionUrl);

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        var payload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = "Curso Instruções",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(orderId, body.GetProperty("orderId").GetGuid());
        Assert.Equal("pix-instructions", body.GetProperty("kind").GetString());
        Assert.Equal(instructionUrl, body.GetProperty("paymentUrl").GetString());

        // Gateway OpenAsync MUST NOT have been called
        app.GatewayMock.Verify(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify that instruction URL is NOT persisted anywhere in database
        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentInDb = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.DoesNotContain(instructionUrl, $"{paymentInDb.GatewayReference} {paymentInDb.SessionReference} {paymentInDb.Status} {paymentInDb.Method}");

        var outboxMessages = await dbCheck.OutboxMessages.ToListAsync(Cancellation);
        foreach (var msg in outboxMessages)
        {
            Assert.DoesNotContain(instructionUrl, msg.Payload);
        }
    }

    [Fact(DisplayName = nameof(CheckoutSessionAsyncPaymentSucceededMarksConfirmedAndProducesFact))]
    public async Task CheckoutSessionAsyncPaymentSucceededMarksConfirmedAndProducesFact()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_async_flow_1";
        var paymentRef = "pi_async_flow_1";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Async"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("pix", paymentRef, expiresAt);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync(paymentRef, It.IsAny<CancellationToken>())).ReturnsAsync("pix");

        using var client = app.CreateClient();
        var eventId = $"evt_async_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.async_payment_succeeded",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = "paid",
                    payment_intent = paymentRef,
                    payment_method_types = new[] { "card", "pix", "boleto" },
                    amount_total = 49700,
                    currency = "brl",
                    metadata = new
                    {
                        tenantId = TenantId.ToString("D"),
                        orderId = orderId.ToString("D")
                    }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        var processedCount = await store.ProcessPendingAsync(Cancellation);
        Assert.True(processedCount >= 1);

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var confirmedPayment = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("confirmed", confirmedPayment.Status);
        Assert.NotNull(confirmedPayment.ConfirmedAt);
        Assert.Equal("pix", confirmedPayment.Method);

        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(AwaitingWebhookAfterConfirmedPaymentIsIgnored))]
    public async Task AwaitingWebhookAfterConfirmedPaymentIsIgnored()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_already_confirmed_1";
        var paymentRef = "pi_already_confirmed_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Já Confirmado"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.Confirm(paymentRef, "card", DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync(paymentRef, It.IsAny<CancellationToken>())).ReturnsAsync("pix");

        using var client = app.CreateClient();
        var eventId = $"evt_late_awaiting_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.completed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = "unpaid",
                    payment_intent = paymentRef,
                    payment_method_types = new[] { "card", "pix", "boleto" },
                    amount_total = 49700,
                    currency = "brl",
                    metadata = new
                    {
                        tenantId = TenantId.ToString("D"),
                        orderId = orderId.ToString("D")
                    }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        await store.ProcessPendingAsync(Cancellation);

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentInDb = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("confirmed", paymentInDb.Status);

        var awaitingFact = await dbCheck.OutboxMessages.FirstOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation);
        Assert.Null(awaitingFact);
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionForAwaitingBoletoReturnsBoletoInstructions))]
    public async Task EnsurePaymentSessionForAwaitingBoletoReturnsBoletoInstructions()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_bol_instructions_1";
        var paymentRef = "pi_bol_instructions_1";
        var instructionUrl = "https://pay.stripe.com/receipts/boleto_instructions_test_url";
        var expiresAt = DateTimeOffset.UtcNow.AddDays(3);

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Boleto Instruções"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("boleto", paymentRef, expiresAt);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock
            .Setup(g => g.GetInstructionsUrlAsync(paymentRef, "boleto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instructionUrl);

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));

        var payload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = "Curso Boleto Instruções",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(orderId, body.GetProperty("orderId").GetGuid());
        Assert.Equal("boleto-instructions", body.GetProperty("kind").GetString());
        Assert.Equal(instructionUrl, body.GetProperty("paymentUrl").GetString());

        app.GatewayMock.Verify(g => g.OpenAsync(It.IsAny<GatewaySessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<HttpResponseMessage> PostSessionEventAsync(string type, string sessionRef, string paymentRef, Guid orderId, string paymentStatus)
    {
        using var client = app.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.CreateVersion7():N}",
            type,
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = paymentStatus,
                    payment_intent = paymentRef,
                    payment_method_types = new[] { "card", "pix", "boleto" },
                    payment_method_options = new { pix = new { expires_after_seconds = 86400 }, boleto = new { expires_after_days = 3 } },
                    amount_total = 49700,
                    currency = "brl",
                    metadata = new { tenantId = TenantId.ToString("D"), orderId = orderId.ToString("D") }
                }
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        return await client.SendAsync(request, Cancellation);
    }

    private async Task<Guid> SeedOpenPaymentAsync(string sessionRef)
    {
        var orderId = Guid.CreateVersion7();
        await using var db = await app.CreateDbContextAsync(TenantId);
        var payment = Payment.Create(TenantId, orderId, new PaymentTerms(Guid.CreateVersion7(), 49700, "BRL", "Curso"));
        payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
        db.Payments.Add(payment);
        await db.SaveChangesAsync(Cancellation);
        return orderId;
    }

    private async Task ProcessAsync()
    {
        await using var scope = app.CreateScope(TenantId);
        await scope.ServiceProvider.GetRequiredService<IPaymentStore>().ProcessPendingAsync(Cancellation);
    }

    [Fact(DisplayName = nameof(BoletoGeneratedThenConfirmedAsyncKeepsBoletoMethodInBothFacts))]
    public async Task BoletoGeneratedThenConfirmedAsyncKeepsBoletoMethodInBothFacts()
    {
        var orderId = await SeedOpenPaymentAsync("cs_bol_async_1");
        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync("pi_bol_async_1", It.IsAny<CancellationToken>())).ReturnsAsync("boleto");

        Assert.Equal(HttpStatusCode.OK, (await PostSessionEventAsync("checkout.session.completed", "cs_bol_async_1", "pi_bol_async_1", orderId, "unpaid")).StatusCode);
        await ProcessAsync();
        Assert.Equal(HttpStatusCode.OK, (await PostSessionEventAsync("checkout.session.async_payment_succeeded", "cs_bol_async_1", "pi_bol_async_1", orderId, "paid")).StatusCode);
        await ProcessAsync();

        await using var db = await app.CreateDbContextAsync(TenantId);
        var payment = await db.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("confirmed", payment.Status);
        Assert.Equal("boleto", payment.Method);
        var awaiting = JsonDocument.Parse((await db.OutboxMessages.SingleAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation)).Payload).RootElement;
        var confirmed = JsonDocument.Parse((await db.OutboxMessages.SingleAsync(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1", Cancellation)).Payload).RootElement;
        Assert.Equal("boleto", awaiting.GetProperty("method").GetString());
        Assert.Equal("boleto", confirmed.GetProperty("method").GetString());
    }

    [Fact(DisplayName = nameof(AsyncConfirmationKeepsTheMethodAlreadyRecordedOnThePayment))]
    public async Task AsyncConfirmationKeepsTheMethodAlreadyRecordedOnThePayment()
    {
        var orderId = await SeedOpenPaymentAsync("cs_keep_method_1");
        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = await db.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
            payment.MarkAwaiting("boleto", "pi_keep_method_1", DateTimeOffset.UtcNow.AddDays(3));
            await db.SaveChangesAsync(Cancellation);
        }
        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync("pi_keep_method_1", It.IsAny<CancellationToken>())).ReturnsAsync("pix");

        Assert.Equal(HttpStatusCode.OK, (await PostSessionEventAsync("checkout.session.async_payment_succeeded", "cs_keep_method_1", "pi_keep_method_1", orderId, "paid")).StatusCode);
        await ProcessAsync();

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        Assert.Equal("boleto", (await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation)).Method);
        var confirmed = JsonDocument.Parse((await dbCheck.OutboxMessages.SingleAsync(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1", Cancellation)).Payload).RootElement;
        Assert.Equal("boleto", confirmed.GetProperty("method").GetString());
    }

    [Fact(DisplayName = nameof(CardPaidSessionWithAllMethodsAllowedStaysCardWithoutAskingTheGateway))]
    public async Task CardPaidSessionWithAllMethodsAllowedStaysCardWithoutAskingTheGateway()
    {
        var orderId = await SeedOpenPaymentAsync("cs_card_real_1");

        Assert.Equal(HttpStatusCode.OK, (await PostSessionEventAsync("checkout.session.completed", "cs_card_real_1", "pi_card_real_1", orderId, "paid")).StatusCode);
        await ProcessAsync();

        await using var db = await app.CreateDbContextAsync(TenantId);
        Assert.Equal("card", (await db.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation)).Method);
        var confirmed = JsonDocument.Parse((await db.OutboxMessages.SingleAsync(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1", Cancellation)).Payload).RootElement;
        Assert.Equal("card", confirmed.GetProperty("method").GetString());
        app.GatewayMock.Verify(g => g.GetPaymentMethodAsync("pi_card_real_1", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = nameof(AwaitingEventIsRefusedForRetryWhenGatewayCannotTellTheMethod))]
    public async Task AwaitingEventIsRefusedForRetryWhenGatewayCannotTellTheMethod()
    {
        var orderId = await SeedOpenPaymentAsync("cs_unavail_1");
        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync("pi_unavail_1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CodeForCoders.Billing.Application.Exceptions.GatewayUnavailableException());

        var response = await PostSessionEventAsync("checkout.session.completed", "cs_unavail_1", "pi_unavail_1", orderId, "unpaid");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var db = await app.CreateDbContextAsync(TenantId);
        Assert.Empty(await db.GatewayInboxEntries.ToListAsync(Cancellation));
    }
}
