using System.Net;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class PendingPaymentOrderTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private OrderFixture Fixture()
    {
        var tenant = Guid.CreateVersion7();
        var factory = hosts.Showcase(tenant);
        factory.BillingClient.Reset();
        return new(factory, tenant);
    }

    [Fact(DisplayName = nameof(ReceivingPaymentAwaitingFactOnAwaitingOrderPopulatesPendingPaymentAndClearsPageExpiresAt))]
    public async Task ReceivingPaymentAwaitingFactOnAwaitingOrderPopulatesPendingPaymentAndClearsPageExpiresAt()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-pix-awaiting");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        // Start payment so paymentPageExpiresAt is populated
        using var client = f.Client();
        using var payResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/payment-session", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);

        // Apply cobranca.pagamento-aguardando.v1 fact
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var fact = new PaymentAwaitingFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "pix",
            expiresAt,
            "pi_pix_test_awaiting_1",
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAwaitingAsync(fact, OrderFixture.Cancellation);
        }

        // Fetch order and assert fields
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("awaiting-payment", readOrder.GetProperty("status").GetString());
        Assert.Equal("pix", readOrder.GetProperty("paymentMethod").GetString());
        Assert.Equal(JsonValueKind.Null, readOrder.GetProperty("paymentPageExpiresAt").ValueKind);

        var pendingPayment = readOrder.GetProperty("pendingPayment");
        Assert.Equal(JsonValueKind.Object, pendingPayment.ValueKind);
        Assert.Equal("pix", pendingPayment.GetProperty("method").GetString());
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), pendingPayment.GetProperty("expiresAt").GetDateTimeOffset().ToUnixTimeSeconds());

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");
    }

    [Fact(DisplayName = nameof(ReceivingPaymentAwaitingFactOnAlreadyPaidOrderHasNoEffect))]
    public async Task ReceivingPaymentAwaitingFactOnAlreadyPaidOrderHasNoEffect()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-paid-before-awaiting");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var confirmedAt = DateTimeOffset.UtcNow;
        var paidFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "card",
            49700,
            "BRL",
            "pi_paid_card_1",
            confirmedAt,
            confirmedAt);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paidFact, OrderFixture.Cancellation);
        }

        // Out-of-order: payment awaiting arrives after paid
        var awaitingFact = new PaymentAwaitingFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "pix",
            DateTimeOffset.UtcNow.AddHours(24),
            "pi_late_awaiting",
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAwaitingAsync(awaitingFact, OrderFixture.Cancellation);
        }

        // Fetch order: must remain paid with card and no pending payment
        using var client = f.Client();
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("paid", readOrder.GetProperty("status").GetString());
        Assert.Equal("card", readOrder.GetProperty("paymentMethod").GetString());
        Assert.Equal(JsonValueKind.Null, readOrder.GetProperty("pendingPayment").ValueKind);

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");
    }

    [Fact(DisplayName = nameof(ResumingAwaitingOrderViaStartOrderPaymentReturnsInstructionsAndPreservesOrderState))]
    public async Task ResumingAwaitingOrderViaStartOrderPaymentReturnsInstructionsAndPreservesOrderState()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-resume-instructions");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var fact = new PaymentAwaitingFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "pix",
            expiresAt,
            "pi_pix_test_resume",
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAwaitingAsync(fact, OrderFixture.Cancellation);
        }

        var expectedInstructionUrl = "https://pay.stripe.com/receipts/pix_resume_instruction";
        f.Factory.BillingClient.Handler = (req, _) =>
        {
            return Task.FromResult(new BillingPaymentSession(
                req.OrderId,
                "pix-instructions",
                expectedInstructionUrl,
                "pix",
                expiresAt));
        };

        using var client = f.Client();
        using var payResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/payment-session", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);

        var payBody = await OrderFixture.BodyAsync(payResp);
        Assert.Equal(orderId, payBody.GetProperty("orderId").GetGuid());
        Assert.Equal("pix-instructions", payBody.GetProperty("kind").GetString());
        Assert.Equal(expectedInstructionUrl, payBody.GetProperty("paymentUrl").GetString());

        // Order remains awaiting-payment, paymentPageExpiresAt is still null, pendingPayment intact
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("awaiting-payment", readOrder.GetProperty("status").GetString());
        Assert.Equal("pix", readOrder.GetProperty("paymentMethod").GetString());
        Assert.Equal(JsonValueKind.Null, readOrder.GetProperty("paymentPageExpiresAt").ValueKind);
        Assert.Equal("pix", readOrder.GetProperty("pendingPayment").GetProperty("method").GetString());

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");
    }

    [Fact(DisplayName = nameof(BoletoGeneratedAndConfirmedLaterStartsGrantValidityOnSettlementDate))]
    public async Task BoletoGeneratedAndConfirmedLaterStartsGrantValidityOnSettlementDate()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        var courseFact = PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(f.Tenant, ids[0], 1, rich: true, level: "beginner"));
        await using (var scope = f.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<IEntitlementCourseProjectionStore>().ApplyAsync(courseFact, OrderFixture.Cancellation);
        }
        using var createResp = await f.CreateAsync(ids[1], "key-boleto-grant-flow");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        // 05/10: Boleto generated
        var boletoOccurredAt = DateTimeOffset.Parse("2026-10-05T14:00:00Z");
        var boletoExpiresAt = DateTimeOffset.Parse("2026-10-09T02:59:59Z");
        var awaitingFact = new PaymentAwaitingFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "boleto",
            boletoExpiresAt,
            "pi_boleto_settle_1",
            boletoOccurredAt);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAwaitingAsync(awaitingFact, OrderFixture.Cancellation);
        }

        // 08/10: Boleto confirmed on settlement date
        var settlementAt = DateTimeOffset.Parse("2026-10-08T10:30:00Z");
        var confirmedFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "boleto",
            49700,
            "BRL",
            "pi_boleto_settle_1",
            settlementAt,
            settlementAt);

        PurchaseCompletedFact? purchaseFact = null;
        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(confirmedFact, OrderFixture.Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var outboxMsg = await db.OutboxMessages.SingleAsync(m => m.RoutingKey == "vendas.compra-concluida.v1", OrderFixture.Cancellation);
            purchaseFact = JsonSerializer.Deserialize<PurchaseCompletedFact>(outboxMsg.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        Assert.NotNull(purchaseFact);
        Assert.Equal("boleto", purchaseFact.PaymentMethod);
        Assert.Equal(settlementAt, purchaseFact.PaidAt);

        // Process purchase fact in Entitlement
        await using (var scope = f.Scope())
        {
            var grantSink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();
            await grantSink.ApplyAsync(purchaseFact, OrderFixture.Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var grant = await db.AccessGrants.SingleAsync(g => g.OriginRef == orderId, OrderFixture.Cancellation);

            Assert.Equal("purchase", grant.Origin);
            Assert.Equal(orderId, grant.OriginRef);
            // RN-D04: 12 months access starts on grant / settlement date (2026-10-08), ending in 2027-10-08
            Assert.Equal(new DateOnly(2027, 10, 8), grant.EndsOn);
        }
    }

    [Fact(DisplayName = nameof(ReceivingBoletoPaymentAwaitingFactPopulatesBoletoMethodAndPendingPayment))]
    public async Task ReceivingBoletoPaymentAwaitingFactPopulatesBoletoMethodAndPendingPayment()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-boleto-awaiting");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var expiresAt = DateTimeOffset.Parse("2026-10-09T02:59:59Z");
        var fact = new PaymentAwaitingFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "boleto",
            expiresAt,
            "pi_boleto_test_1",
            DateTimeOffset.Parse("2026-10-06T10:01:12Z"));

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAwaitingAsync(fact, OrderFixture.Cancellation);
        }

        using var client = f.Client();
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("awaiting-payment", readOrder.GetProperty("status").GetString());
        Assert.Equal("boleto", readOrder.GetProperty("paymentMethod").GetString());
        var pendingPayment = readOrder.GetProperty("pendingPayment");
        Assert.Equal("boleto", pendingPayment.GetProperty("method").GetString());
        Assert.Equal(expiresAt, pendingPayment.GetProperty("expiresAt").GetDateTimeOffset());

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");
    }
}
