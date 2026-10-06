using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Contracts;
using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Infra.Data;
using CodeForCoders.Billing.Infra.Messaging;
using CodeForCoders.Billing.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Billing.IntegrationTests;

[Collection(BillingIntegrationCollection.Name)]
public sealed class PaymentCancellationTests(BillingIntegrationFixture fixture) : IAsyncLifetime
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

    [Fact(DisplayName = nameof(WebhookSessionExpiredMarksNotConfirmedAndProducesOutboxFact))]
    public async Task WebhookSessionExpiredMarksNotConfirmedAndProducesOutboxFact()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_expired_flow_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Expiração"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = app.CreateClient();
        var eventId = $"evt_exp_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.expired",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = "unpaid",
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
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("not-confirmed", paymentAfter.Status);
        Assert.Equal("expired", paymentAfter.Reason);

        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        Assert.Equal("PagamentoNaoConfirmado", outboxMessage.Type);
        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(WebhookAsyncPaymentFailedMarksNotConfirmedAndProducesOutboxFact))]
    public async Task WebhookAsyncPaymentFailedMarksNotConfirmedAndProducesOutboxFact()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_fail_flow_1";
        var paymentRef = "pi_fail_flow_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Falha Async"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("boleto", paymentRef, DateTimeOffset.UtcNow.AddDays(3));
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = app.CreateClient();
        var eventId = $"evt_fail_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.async_payment_failed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = "unpaid",
                    payment_intent = paymentRef,
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
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("not-confirmed", paymentAfter.Status);
        Assert.Equal("expired", paymentAfter.Reason);

        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(OrderCancelledConsumerCancelsPaymentAndProducesOutboxFact))]
    public async Task OrderCancelledConsumerCancelsPaymentAndProducesOutboxFact()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_cancel_flow_1";
        var paymentRef = "pi_cancel_flow_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Cancelar"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("pix", paymentRef, DateTimeOffset.UtcNow.AddHours(24));
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.ExpireSessionAsync(sessionRef, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        app.GatewayMock.Setup(g => g.CancelPaymentIntentAsync(paymentRef, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Run consumer worker
        await PublishAndConsumeOrderCancelledAsync(new OrderCancelledV1(Guid.CreateVersion7(), TenantId, orderId, studentId, DateTimeOffset.UtcNow));

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("not-confirmed", paymentAfter.Status);
        Assert.Equal("cancelled", paymentAfter.Reason);

        app.GatewayMock.Verify(g => g.ExpireSessionAsync(sessionRef, It.IsAny<CancellationToken>()), Times.Once());
        app.GatewayMock.Verify(g => g.CancelPaymentIntentAsync(paymentRef, It.IsAny<CancellationToken>()), Times.Once());

        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        var payloadElement = JsonDocument.Parse(outboxMessage.Payload).RootElement;
        Assert.Equal("cancelled", payloadElement.GetProperty("reason").GetString());
        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(OrderCancelledIgnoresAlreadyConfirmedPayment))]
    public async Task OrderCancelledIgnoresAlreadyConfirmedPayment()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_already_paid_1";
        var paymentRef = "pi_already_paid_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Já Pago"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.Confirm(paymentRef, "card", DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        await PublishAndConsumeOrderCancelledAsync(new OrderCancelledV1(Guid.CreateVersion7(), TenantId, orderId, studentId, DateTimeOffset.UtcNow));

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("confirmed", paymentAfter.Status);
        Assert.Null(paymentAfter.Reason);

        var outboxCount = await dbCheck.OutboxMessages.CountAsync(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1", Cancellation);
        Assert.Equal(0, outboxCount);
    }

    [Fact(DisplayName = nameof(OrderCancellationIsIdempotentWhenRepeated))]
    public async Task OrderCancellationIsIdempotentWhenRepeated()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_idem_cancel_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Cancelar Idem"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkNotConfirmed("cancelled");
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        await PublishAndConsumeOrderCancelledAsync(new OrderCancelledV1(Guid.CreateVersion7(), TenantId, orderId, studentId, DateTimeOffset.UtcNow));

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var outboxCount = await dbCheck.OutboxMessages.CountAsync(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1", Cancellation);
        Assert.Equal(0, outboxCount);
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionReturns422WhenPaymentIsCancelled))]
    public async Task EnsurePaymentSessionReturns422WhenPaymentIsCancelled()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_canc_session_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Cancelado"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkNotConfirmed("cancelled");
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = app.CreateClient();
        var token = app.CreateCommerceAssertion(TenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = "Curso Cancelado",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PAYMENT_CANCELLED", problem.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(EnsurePaymentSessionReturns422WhenPaymentIsExpired))]
    public async Task EnsurePaymentSessionReturns422WhenPaymentIsExpired()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_exp_session_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Expirado"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-25));
            payment.MarkNotConfirmed("expired");
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = app.CreateClient();
        var token = app.CreateCommerceAssertion(TenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = "Curso Expirado",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };

        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PAYMENT_EXPIRED", problem.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(LatePaymentConfirmationAfterCancellationStillConfirmsPayment))]
    public async Task LatePaymentConfirmationAfterCancellationStillConfirmsPayment()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_late_conf_1";
        var paymentRef = "pi_late_conf_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Tardio"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("boleto", paymentRef, DateTimeOffset.UtcNow.AddDays(3));
            payment.MarkNotConfirmed("cancelled");
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync(paymentRef, It.IsAny<CancellationToken>())).ReturnsAsync("boleto");

        using var client = app.CreateClient();
        var eventId = $"evt_late_{Guid.CreateVersion7():N}";
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
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("confirmed", paymentAfter.Status);
        Assert.NotNull(paymentAfter.ConfirmedAt);

        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(OrderCancelledRedeliversAndDeadLettersWhenTheGatewayIsDown))]
    public async Task OrderCancelledRedeliversAndDeadLettersWhenTheGatewayIsDown()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_down_flow_1";
        var paymentRef = "pi_down_flow_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Gateway Fora"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("pix", paymentRef, DateTimeOffset.UtcNow.AddHours(24));
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.ExpireSessionAsync(sessionRef, It.IsAny<CancellationToken>())).ThrowsAsync(new GatewayUnavailableException());

        await PublishAndConsumeOrderCancelledAsync(
            new OrderCancelledV1(Guid.CreateVersion7(), TenantId, orderId, studentId, DateTimeOffset.UtcNow), TimeSpan.FromSeconds(7));

        // The page or PIX is still live on the gateway, so the payment must not be recorded as cancelled.
        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("awaiting", paymentAfter.Status);
        Assert.Null(paymentAfter.Reason);
        Assert.Equal(0, await dbCheck.OutboxMessages.CountAsync(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1", Cancellation));

        // Delivered up to the delivery limit, then parked on the reprocessable dead-letter queue.
        var resourceNames = app.Services.GetRequiredService<RabbitMqResourceNames>();
        app.GatewayMock.Verify(g => g.ExpireSessionAsync(sessionRef, It.IsAny<CancellationToken>()), Times.Exactly(3));
        app.GatewayMock.Verify(g => g.CancelPaymentIntentAsync(paymentRef, It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal(1u, await DeadLetterCountAsync($"{resourceNames.OrderCancellationsQueue}.dlq"));
    }

    [Fact(DisplayName = nameof(SessionExpiredWebhookAfterCancellationDoesNotRewriteTheTerminalReasonNorPublishASecondFact))]
    public async Task SessionExpiredWebhookAfterCancellationDoesNotRewriteTheTerminalReasonNorPublishASecondFact()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_cancel_then_expired_1";
        var paymentRef = "pi_cancel_then_expired_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Cancelar e Expirar"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            payment.MarkAwaiting("pix", paymentRef, DateTimeOffset.UtcNow.AddHours(24));
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.ExpireSessionAsync(sessionRef, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        app.GatewayMock.Setup(g => g.CancelPaymentIntentAsync(paymentRef, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await PublishAndConsumeOrderCancelledAsync(new OrderCancelledV1(Guid.CreateVersion7(), TenantId, orderId, studentId, DateTimeOffset.UtcNow));

        // Stripe answers the expire call with checkout.session.expired.
        using var client = app.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            id = $"evt_after_cancel_{Guid.CreateVersion7():N}",
            type = "checkout.session.expired",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = sessionRef,
                    payment_status = "unpaid",
                    metadata = new { tenantId = TenantId.ToString("D"), orderId = orderId.ToString("D") }
                }
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, Cancellation)).StatusCode);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        Assert.True(await store.ProcessPendingAsync(Cancellation) >= 1);

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("not-confirmed", paymentAfter.Status);
        Assert.Equal("cancelled", paymentAfter.Reason);

        var facts = await dbCheck.OutboxMessages.Where(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1").ToListAsync(Cancellation);
        var fact = Assert.Single(facts);
        Assert.Equal("cancelled", JsonDocument.Parse(fact.Payload).RootElement.GetProperty("reason").GetString());
    }

    [Fact(DisplayName = nameof(DelayedAwaitingWebhookAfterCancellationDoesNotReopenThePaymentNorAllowResuming))]
    public async Task DelayedAwaitingWebhookAfterCancellationDoesNotReopenThePaymentNorAllowResuming()
    {
        var (orderId, studentId, sessionRef, paymentRef) = await SeedCancelledOpenPaymentAsync("cs_cancel_then_awaiting_1", "pi_cancel_then_awaiting_1");

        await PostGatewayWebhookAsync("checkout.session.completed", sessionRef, orderId, "unpaid", paymentRef);
        await ProcessInboxAsync();

        await using (var dbCheck = await app.CreateDbContextAsync(TenantId))
        {
            var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
            Assert.Equal("not-confirmed", paymentAfter.Status);
            Assert.Equal("cancelled", paymentAfter.Reason);
            Assert.Equal(0, await dbCheck.OutboxMessages.CountAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation));
            Assert.Equal(1, await dbCheck.OutboxMessages.CountAsync(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1", Cancellation));
        }

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateCommerceAssertion(TenantId));
        var payload = new
        {
            studentId,
            amountCents = 49700,
            currency = "BRL",
            description = "Curso Cancelar e Aguardar",
            successUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido",
            cancelUrl = $"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu"
        };
        using var response = await client.PutAsJsonAsync($"/internal/v1/payment-sessions/{orderId:D}", payload, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PAYMENT_CANCELLED", problem.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(AwaitingThenExpiredWebhooksAfterCancellationKeepTheReasonAndPublishNoExtraFact))]
    public async Task AwaitingThenExpiredWebhooksAfterCancellationKeepTheReasonAndPublishNoExtraFact()
    {
        var (orderId, _, sessionRef, paymentRef) = await SeedCancelledOpenPaymentAsync("cs_cancel_awaiting_expired_1", "pi_cancel_awaiting_expired_1");

        await PostGatewayWebhookAsync("checkout.session.completed", sessionRef, orderId, "unpaid", paymentRef);
        await ProcessInboxAsync();
        await PostGatewayWebhookAsync("checkout.session.expired", sessionRef, orderId, "unpaid", null);
        await ProcessInboxAsync();

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("not-confirmed", paymentAfter.Status);
        Assert.Equal("cancelled", paymentAfter.Reason);
        Assert.Equal(0, await dbCheck.OutboxMessages.CountAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation));
        var fact = Assert.Single(await dbCheck.OutboxMessages.Where(m => m.RoutingKey == "cobranca.pagamento-nao-confirmado.v1").ToListAsync(Cancellation));
        Assert.Equal("cancelled", JsonDocument.Parse(fact.Payload).RootElement.GetProperty("reason").GetString());
    }

    [Fact(DisplayName = nameof(LateConfirmationAfterCancellationAndDelayedAwaitingStillConfirmsPayment))]
    public async Task LateConfirmationAfterCancellationAndDelayedAwaitingStillConfirmsPayment()
    {
        var (orderId, _, sessionRef, paymentRef) = await SeedCancelledOpenPaymentAsync("cs_cancel_awaiting_paid_1", "pi_cancel_awaiting_paid_1");
        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync(paymentRef, It.IsAny<CancellationToken>())).ReturnsAsync("pix");

        await PostGatewayWebhookAsync("checkout.session.completed", sessionRef, orderId, "unpaid", paymentRef);
        await ProcessInboxAsync();
        await PostGatewayWebhookAsync("checkout.session.async_payment_succeeded", sessionRef, orderId, "paid", paymentRef);
        await ProcessInboxAsync();

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var paymentAfter = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("confirmed", paymentAfter.Status);
        Assert.NotNull(paymentAfter.ConfirmedAt);
        Assert.Equal(0, await dbCheck.OutboxMessages.CountAsync(m => m.RoutingKey == "cobranca.pagamento-aguardando.v1", Cancellation));
        var confirmed = Assert.Single(await dbCheck.OutboxMessages.Where(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1").ToListAsync(Cancellation));
        BillingMessages.AssertSends(confirmed.RoutingKey, confirmed.Payload);
    }

    // An open payment whose order was cancelled through the real consumer: not-confirmed/cancelled plus its single fact.
    private async Task<(Guid OrderId, Guid StudentId, string SessionRef, string PaymentRef)> SeedCancelledOpenPaymentAsync(string sessionRef, string paymentRef)
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Cancelar e Aguardar"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        app.GatewayMock.Setup(g => g.ExpireSessionAsync(sessionRef, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        app.GatewayMock.Setup(g => g.GetPaymentMethodAsync(paymentRef, It.IsAny<CancellationToken>())).ReturnsAsync("pix");
        await PublishAndConsumeOrderCancelledAsync(new OrderCancelledV1(Guid.CreateVersion7(), TenantId, orderId, studentId, DateTimeOffset.UtcNow));

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var seeded = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.Equal("not-confirmed", seeded.Status);
        Assert.Equal("cancelled", seeded.Reason);
        return (orderId, studentId, sessionRef, paymentRef);
    }

    private async Task PostGatewayWebhookAsync(string type, string sessionRef, Guid orderId, string paymentStatus, string? paymentRef)
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
                    amount_total = 49700,
                    currency = "brl",
                    metadata = new { tenantId = TenantId.ToString("D"), orderId = orderId.ToString("D") }
                }
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, Cancellation)).StatusCode);
    }

    private async Task ProcessInboxAsync()
    {
        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        Assert.True(await store.ProcessPendingAsync(Cancellation) >= 1);
    }

    private async Task<uint> DeadLetterCountAsync(string deadLetterQueue)
    {
        var factory = new ConnectionFactory
        {
            HostName = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            UserName = "code_for_coders",
            Password = "code_for_coders",
            VirtualHost = "/"
        };
        await using var connection = await factory.CreateConnectionAsync(Cancellation);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Cancellation);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var declared = await channel.QueueDeclarePassiveAsync(deadLetterQueue, Cancellation);
            if (declared.MessageCount > 0) return declared.MessageCount;
            await Task.Delay(500, Cancellation);
        }
        return 0;
    }

    private async Task PublishAndConsumeOrderCancelledAsync(OrderCancelledV1 message, TimeSpan? runFor = null)
    {
        var duration = runFor ?? TimeSpan.FromSeconds(1);
        var factory = new ConnectionFactory
        {
            HostName = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            UserName = "code_for_coders",
            Password = "code_for_coders",
            VirtualHost = "/"
        };

        var connectionProvider = app.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var resourceNames = app.Services.GetRequiredService<RabbitMqResourceNames>();
        var rabbitOptions = app.Services.GetRequiredService<IOptions<RabbitMqOptions>>();
        var initializer = new RabbitMqTopologyInitializer(connectionProvider, resourceNames);
        await initializer.StartAsync(Cancellation);

        await using var connection = await factory.CreateConnectionAsync(Cancellation);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Cancellation);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            exchange: resourceNames.CommerceExchange,
            routingKey: "vendas.pedido-cancelado.v1",
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: Cancellation);

        // Run worker once to consume
        using var cts = new CancellationTokenSource(duration + TimeSpan.FromSeconds(5));
        var worker = new OrderCancellationConsumerWorker(
            app.Services.GetRequiredService<RabbitMqConnectionProvider>(),
            app.Services.GetRequiredService<IServiceScopeFactory>(),
            resourceNames,
            Options.Create(new RabbitMqOptions
            {
                // Short limit keeps the backoff (1 s, 2 s) inside the test window.
                DeliveryLimit = 3,
                PrefetchCount = rabbitOptions.Value.PrefetchCount
            }),
            NullLogger<OrderCancellationConsumerWorker>.Instance);

        await worker.StartAsync(cts.Token);
        await Task.Delay(duration, Cancellation);
        await worker.StopAsync(CancellationToken.None);
    }
}
