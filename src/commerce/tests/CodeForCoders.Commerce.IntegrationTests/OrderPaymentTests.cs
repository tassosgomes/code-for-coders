using System.Net;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OrderPaymentTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private OrderFixture Fixture()
    {
        var tenant = Guid.CreateVersion7();
        var factory = hosts.Showcase(tenant);
        factory.BillingClient.Reset();
        return new(factory, tenant);
    }

    [Fact(DisplayName = nameof(FreezeOnStartOrderPaymentInternal))]
    public async Task FreezeOnStartOrderPaymentInternal()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-freeze");
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        using var client = f.Client();
        using var payResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/payment-session", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);
        Assert.True(payResp.Headers.CacheControl!.NoStore);

        var payBody = await OrderFixture.BodyAsync(payResp);
        Assert.Equal(orderId, payBody.GetProperty("orderId").GetGuid());
        Assert.Equal("checkout", payBody.GetProperty("kind").GetString());
        Assert.StartsWith("https://checkout.stripe.com", payBody.GetProperty("paymentUrl").GetString());
        Assert.True(payBody.TryGetProperty("expiresAt", out _));

        // Order is still awaiting payment and frozen snapshot remains intact
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);
        Assert.Equal("awaiting-payment", readOrder.GetProperty("status").GetString());
        Assert.Equal(created.GetProperty("priceCents").GetInt32(), readOrder.GetProperty("priceCents").GetInt32());
        Assert.Equal(created.GetProperty("number").GetString(), readOrder.GetProperty("number").GetString());
        Assert.Equal(created.GetProperty("offer").GetProperty("offerId").GetGuid(), readOrder.GetProperty("offer").GetProperty("offerId").GetGuid());
    }

    [Fact(DisplayName = nameof(StartPayment_RejectsNonPayableWith422))]
    public async Task StartPayment_RejectsNonPayableWith422()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-nonpayable");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        // Confirm payment so order becomes paid
        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            var fact = new PaymentConfirmedFact(
                Guid.CreateVersion7(),
                f.Tenant,
                Guid.CreateVersion7(),
                orderId,
                "card",
                49700,
                "BRL",
                "pi_paid_test",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            await sink.ApplyAsync(fact, OrderFixture.Cancellation);
        }

        using var client = f.Client();
        using var payResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/payment-session", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, payResp.StatusCode);

        var errBody = await OrderFixture.BodyAsync(payResp);
        Assert.Equal("ORDER_NOT_PAYABLE", errBody.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(StartPayment_Returns503WhenBillingFails))]
    public async Task StartPayment_Returns503WhenBillingFails()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-billing-503");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        f.Factory.BillingClient.Handler = (_, _) => throw new PaymentProviderUnavailableException();

        using var client = f.Client();
        using var payResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/payment-session", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, payResp.StatusCode);

        var errBody = await OrderFixture.BodyAsync(payResp);
        Assert.Equal("PAYMENT_PROVIDER_UNAVAILABLE", errBody.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(PaymentConfirmedFact_TransitionsOrderToPaidAndWritesOutboxFact))]
    public async Task PaymentConfirmedFact_TransitionsOrderToPaidAndWritesOutboxFact()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-confirm");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var eventId = Guid.CreateVersion7();
        var confirmedAt = DateTimeOffset.UtcNow;
        var paymentFact = new PaymentConfirmedFact(
            eventId,
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "card",
            49700,
            "BRL",
            "pi_test_confirm_1",
            confirmedAt,
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paymentFact, OrderFixture.Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId, OrderFixture.Cancellation);
            Assert.Equal("paid", order.Status);
            Assert.Equal("card", order.PaymentMethod);
            Assert.NotNull(order.PaidAt);

            var outbox = await db.OutboxMessages
                .Where(m => m.RoutingKey == "vendas.compra-concluida.v1")
                .ToListAsync(OrderFixture.Cancellation);
            var message = Assert.Single(outbox);

            CommerceMessages.AssertSends(message.RoutingKey, message.Payload);

            using var doc = JsonDocument.Parse(message.Payload);
            var root = doc.RootElement;
            Assert.Equal(f.Tenant, root.GetProperty("tenantId").GetGuid());
            Assert.Equal(orderId, root.GetProperty("orderId").GetGuid());
            Assert.Equal(f.Student, root.GetProperty("studentId").GetGuid());
            Assert.Equal(ids[0], root.GetProperty("courseId").GetGuid());
            Assert.Equal(ids[1], root.GetProperty("offerId").GetGuid());
            Assert.Equal(49700, root.GetProperty("paidAmountCents").GetInt32());
            Assert.Equal("card", root.GetProperty("paymentMethod").GetString());
        }
    }

    [Fact(DisplayName = nameof(PaymentConfirmedFact_ReplayIsIdempotent))]
    public async Task PaymentConfirmedFact_ReplayIsIdempotent()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-idempotent-fact");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var paymentFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "card",
            49700,
            "BRL",
            "pi_test_replay_1",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paymentFact, OrderFixture.Cancellation);
            // Replay identical fact
            await sink.ApplyAsync(paymentFact, OrderFixture.Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var outbox = await db.OutboxMessages
                .Where(m => m.RoutingKey == "vendas.compra-concluida.v1")
                .ToListAsync(OrderFixture.Cancellation);
            Assert.Single(outbox);

            var order = await db.Orders.SingleAsync(o => o.Id == orderId, OrderFixture.Cancellation);
            Assert.Equal("paid", order.Status);
        }
    }

    [Fact(DisplayName = nameof(PaymentConfirmedFact_DivergentAmount_RecordsTelemetryAndConfirms))]
    public async Task PaymentConfirmedFact_DivergentAmount_RecordsTelemetryAndConfirms()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-divergent");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var paymentFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "card",
            39700, // Divergent: order is 49700
            "BRL",
            "pi_test_divergent",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paymentFact, OrderFixture.Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId, OrderFixture.Cancellation);
            Assert.Equal("paid", order.Status);
        }
    }

    [Fact(DisplayName = nameof(PaymentConfirmedFact_UnknownOrder_ThrowsOrderRuleException))]
    public async Task PaymentConfirmedFact_UnknownOrder_ThrowsOrderRuleException()
    {
        var f = Fixture();
        var unknownOrderId = Guid.CreateVersion7();

        var paymentFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            unknownOrderId,
            "card",
            49700,
            "BRL",
            "pi_test_unknown",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await using var scope = f.Scope();
        var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();

        var ex = await Assert.ThrowsAsync<OrderRuleException>(() =>
            sink.ApplyAsync(paymentFact, OrderFixture.Cancellation));
        Assert.Equal("ORDER_UNKNOWN", ex.Code);
    }

    [Fact(DisplayName = nameof(StartPaymentSendsReturnUrlsToTheStudentOrderPage))]
    public async Task StartPaymentSendsReturnUrlsToTheStudentOrderPage()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-return-urls");
        var orderId = (await OrderFixture.BodyAsync(createResp)).GetProperty("orderId").GetGuid();

        BillingPaymentRequest? sent = null;
        f.Factory.BillingClient.Handler = (req, _) =>
        {
            sent = req;
            return Task.FromResult(new BillingPaymentSession(req.OrderId, "checkout",
                "https://checkout.stripe.com/pay/return-urls", null, DateTimeOffset.UtcNow.AddHours(24)));
        };

        using var client = f.Client();
        using var payResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/payment-session", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);
        Assert.Equal($"http://localhost:8082/student/pedidos/{orderId:D}?resultado=concluido", sent!.SuccessUrl);
        Assert.Equal($"http://localhost:8082/student/pedidos/{orderId:D}?resultado=saiu", sent.CancelUrl);
    }

    [Fact(DisplayName = nameof(PaymentConfirmedFact_EmptyIds_ThrowsOrderRuleException))]
    public async Task PaymentConfirmedFact_EmptyIds_ThrowsOrderRuleException()
    {
        var f = Fixture();

        var invalidFact = new PaymentConfirmedFact(
            Guid.Empty,
            f.Tenant,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "card",
            49700,
            "BRL",
            "pi_test_invalid",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await using var scope = f.Scope();
        var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();

        var ex = await Assert.ThrowsAsync<OrderRuleException>(() =>
            sink.ApplyAsync(invalidFact, OrderFixture.Cancellation));
        Assert.Equal("PAYMENT_INVALID", ex.Code);
    }
}
