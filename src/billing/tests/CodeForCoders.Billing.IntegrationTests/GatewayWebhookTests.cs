using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Billing.IntegrationTests;

[Collection(BillingIntegrationCollection.Name)]
public sealed class GatewayWebhookTests(BillingIntegrationFixture fixture) : IAsyncLifetime
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

    [Fact(DisplayName = nameof(InvalidOrMissingSignatureReturns400AndStoresNothingInInbox))]
    public async Task InvalidOrMissingSignatureReturns400AndStoresNothingInInbox()
    {
        using var client = app.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            id = "evt_invalid_sig_1",
            type = "checkout.session.completed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new { @object = new { id = "cs_test_1" } }
        });

        // Request with missing signature
        using var missingSigRequest = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var missingResponse = await client.SendAsync(missingSigRequest, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        var missingErr = await missingResponse.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("SIGNATURE_INVALID", missingErr.GetProperty("code").GetString());

        // Request with invalid signature
        using var invalidSigRequest = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        invalidSigRequest.Headers.Add("Stripe-Signature", "t=1234567,v1=bad_hex_signature");
        using var invalidResponse = await client.SendAsync(invalidSigRequest, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        // Verify inbox is empty
        await using var db = await app.CreateDbContextAsync(TenantId);
        var count = await db.GatewayInboxEntries.CountAsync(Cancellation);
        Assert.Equal(0, count);
    }

    [Fact(DisplayName = nameof(ValidSignatureRecordsEventInInboxAndReturns200))]
    public async Task ValidSignatureRecordsEventInInboxAndReturns200()
    {
        using var client = app.CreateClient();
        var eventId = $"evt_valid_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.completed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = "cs_valid_1",
                    payment_status = "paid",
                    payment_intent = "pi_valid_1",
                    amount_total = 49700,
                    currency = "brl",
                    metadata = new
                    {
                        tenantId = TenantId.ToString("D"),
                        orderId = Guid.CreateVersion7().ToString("D")
                    }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));

        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = await app.CreateDbContextAsync(TenantId);
        var entry = await db.GatewayInboxEntries.SingleOrDefaultAsync(e => e.Id == eventId, Cancellation);
        Assert.NotNull(entry);
        Assert.Equal("checkout.session.completed", entry.Type);
        Assert.Equal("cs_valid_1", entry.SessionReference);
        Assert.Equal("pi_valid_1", entry.PaymentReference);
        Assert.Equal("confirmed", entry.Outcome);
        Assert.Equal("card", entry.Method);
        Assert.Equal(49700, entry.AmountCents);
    }

    [Fact(DisplayName = nameof(SameEventIdThreeTimesIsIdempotentAndStoresOnce))]
    public async Task SameEventIdThreeTimesIsIdempotentAndStoresOnce()
    {
        using var client = app.CreateClient();
        var eventId = $"evt_idemp_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.completed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = "cs_idemp_1",
                    payment_status = "paid",
                    payment_intent = "pi_idemp_1",
                    amount_total = 49700,
                    currency = "brl"
                }
            }
        });

        var signature = BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret);

        for (var i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Stripe-Signature", signature);

            using var response = await client.SendAsync(request, Cancellation);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using var db = await app.CreateDbContextAsync(TenantId);
        var entries = await db.GatewayInboxEntries.Where(e => e.Id == eventId).ToListAsync(Cancellation);
        Assert.Single(entries);
    }

    [Fact(DisplayName = nameof(InboxRowDoesNotContainCustomerDetailsPii))]
    public async Task InboxRowDoesNotContainCustomerDetailsPii()
    {
        using var client = app.CreateClient();
        var eventId = $"evt_pii_{Guid.CreateVersion7():N}";
        var sensitiveName = "Aluno Segredo Teste";
        var sensitiveEmail = "alunosegredo@dominio-exclusivo.com";
        var sensitiveCpf = "12345678909";

        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "checkout.session.completed",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = "cs_pii_1",
                    payment_status = "paid",
                    payment_intent = "pi_pii_1",
                    amount_total = 49700,
                    currency = "brl",
                    customer_details = new
                    {
                        name = sensitiveName,
                        email = sensitiveEmail,
                        tax_ids = new[] { new { value = sensitiveCpf } },
                        address = new { line1 = "Rua Teste das Flores 123", city = "São Paulo", state = "SP" }
                    }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));

        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Raw database query to assert no column contains sensitive PII
        await using var db = await app.CreateDbContextAsync(TenantId);
        var entry = await db.GatewayInboxEntries.SingleAsync(e => e.Id == eventId, Cancellation);

        // Check entry properties
        Assert.DoesNotContain(sensitiveName, $"{entry.Id} {entry.Type} {entry.ObjectReference} {entry.SessionReference} {entry.PaymentReference}");
        Assert.DoesNotContain(sensitiveEmail, $"{entry.Id} {entry.Type} {entry.ObjectReference} {entry.SessionReference} {entry.PaymentReference}");
        Assert.DoesNotContain(sensitiveCpf, $"{entry.Id} {entry.Type} {entry.ObjectReference} {entry.SessionReference} {entry.PaymentReference}");
    }

    [Fact(DisplayName = nameof(CheckoutSessionCompletedPaidProducesPaymentConfirmedFactInOutbox))]
    public async Task CheckoutSessionCompletedPaidProducesPaymentConfirmedFactInOutbox()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_paid_flow_1";
        var paymentRef = "pi_paid_flow_1";

        // Seed payment aggregate in DB
        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", ".NET do zero à API"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        // Post webhook
        using var client = app.CreateClient();
        var eventId = $"evt_flow_{Guid.CreateVersion7():N}";
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
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Process inbox
        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        var processedCount = await store.ProcessPendingAsync(Cancellation);
        Assert.True(processedCount >= 1);

        // Verify Payment is confirmed
        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var confirmedPayment = await dbCheck.Payments.SingleAsync(p => p.OrderId == orderId, Cancellation);
        Assert.NotNull(confirmedPayment.ConfirmedAt);
        Assert.Equal(paymentRef, confirmedPayment.GatewayReference);

        // Verify outbox message
        var outboxMessage = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1", Cancellation);
        Assert.NotNull(outboxMessage);
        Assert.Equal("PagamentoConfirmado", outboxMessage.Type);
    }

    [Fact(DisplayName = nameof(PaymentConfirmedFactConformsToAsyncApiContract))]
    public async Task PaymentConfirmedFactConformsToAsyncApiContract()
    {
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();
        var sessionRef = "cs_contract_flow_1";
        var paymentRef = "pi_contract_flow_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(studentId, 49700, "BRL", "Curso Contrato"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = app.CreateClient();
        var eventId = $"evt_contract_{Guid.CreateVersion7():N}";
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
        await client.SendAsync(request, Cancellation);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        await store.ProcessPendingAsync(Cancellation);

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var outboxMessage = await dbCheck.OutboxMessages.SingleAsync(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1", Cancellation);

        // Assert conformity against AsyncAPI contract
        BillingMessages.AssertSends(outboxMessage.RoutingKey, outboxMessage.Payload);
    }

    [Fact(DisplayName = nameof(RepeatedProcessingDoesNotProduceDuplicateFact))]
    public async Task RepeatedProcessingDoesNotProduceDuplicateFact()
    {
        var orderId = Guid.CreateVersion7();
        var sessionRef = "cs_repeat_1";
        var paymentRef = "pi_repeat_1";

        await using (var db = await app.CreateDbContextAsync(TenantId))
        {
            var payment = Payment.Create(TenantId, orderId, new PaymentTerms(Guid.CreateVersion7(), 49700, "BRL", "Curso Repeat"));
            payment.Open(sessionRef, DateTimeOffset.UtcNow.AddHours(24), DateTimeOffset.UtcNow);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = app.CreateClient();
        var eventId = $"evt_repeat_{Guid.CreateVersion7():N}";
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
        await client.SendAsync(request, Cancellation);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();

        // First run processes entry
        await store.ProcessPendingAsync(Cancellation);

        // Second run finds no pending entries
        var secondRunCount = await store.ProcessPendingAsync(Cancellation);
        Assert.Equal(0, secondRunCount);

        await using var dbCheck = await app.CreateDbContextAsync(TenantId);
        var messages = await dbCheck.OutboxMessages.Where(m => m.RoutingKey == "cobranca.pagamento-confirmado.v1").ToListAsync(Cancellation);
        Assert.Single(messages);
    }

    [Fact(DisplayName = nameof(UnhandledOrNonPaidEventIsMarkedProcessedWithoutFact))]
    public async Task UnhandledOrNonPaidEventIsMarkedProcessedWithoutFact()
    {
        using var client = app.CreateClient();
        var eventId = $"evt_refund_{Guid.CreateVersion7():N}";
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "charge.refunded",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = "ch_test_refund_1",
                    amount_refunded = 49700,
                    currency = "brl"
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = app.CreateScope(TenantId);
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
        var processedCount = await store.ProcessPendingAsync(Cancellation);
        Assert.Equal(1, processedCount);

        await using var db = await app.CreateDbContextAsync(TenantId);
        var entry = await db.GatewayInboxEntries.SingleAsync(e => e.Id == eventId, Cancellation);
        Assert.NotNull(entry.ProcessedAt);

        var outboxCount = await db.OutboxMessages.CountAsync(Cancellation);
        Assert.Equal(0, outboxCount);
    }

    [Fact(DisplayName = nameof(WebhookCountersTrackTranslatedOutcomeAndRefusedSignaturesWithoutIdentifiers))]
    public async Task WebhookCountersTrackTranslatedOutcomeAndRefusedSignaturesWithoutIdentifiers()
    {
        var received = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
        var refused = new List<(long Value, int Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == BillingTelemetry.MeterName
                && instrument.Name is "billing.gateway.events.received" or "billing.gateway.signatures.refused")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "billing.gateway.events.received") lock (received) received.Add((value, tags.ToArray()));
            else lock (refused) refused.Add((value, tags.Length));
        });
        listener.Start();

        var orderId = Guid.CreateVersion7();
        using var client = app.CreateClient();
        async Task<HttpStatusCode> PostAsync(string type, string status, string? signature = null)
        {
            var body = JsonSerializer.Serialize(new
            {
                id = $"evt_metric_{Guid.CreateVersion7():N}",
                type,
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = new
                {
                    @object = new
                    {
                        id = "cs_metric_1",
                        payment_status = status,
                        payment_intent = "pi_metric_1",
                        amount_total = 49700,
                        currency = "brl",
                        metadata = new { tenantId = TenantId.ToString("D"), orderId = orderId.ToString("D") }
                    }
                }
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/v1/stripe/events")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add("Stripe-Signature", signature ?? BillingTestApp.CreateStripeSignature(body, BillingTestApp.WebhookSecret));
            using var response = await client.SendAsync(request, Cancellation);
            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await PostAsync("checkout.session.completed", "paid"));
        Assert.Equal(HttpStatusCode.OK, await PostAsync("checkout.session.expired", "unpaid"));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync("checkout.session.completed", "paid", "t=1234567,v1=bad"));
        listener.RecordObservableInstruments();

        lock (received)
        {
            Assert.Equal(2, received.Count);
            Assert.All(received, item => Assert.Equal(1, item.Value));
            Assert.Equal(["confirmed", "ignored"], received.Select(item => Assert.Single(item.Tags).Value as string).Order());
            Assert.All(received, item => Assert.Equal("type", Assert.Single(item.Tags).Key));
        }
        lock (refused) Assert.Equal([(1L, 0)], refused);
        var tagText = string.Join(' ', received.SelectMany(item => item.Tags).Select(tag => $"{tag.Key}={tag.Value}"));
        Assert.DoesNotContain(orderId.ToString("D"), tagText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TenantId.ToString("D"), tagText, StringComparison.OrdinalIgnoreCase);
    }
}
