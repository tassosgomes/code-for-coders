using System.Net;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OrderCancellationTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private OrderFixture Fixture()
    {
        var tenant = Guid.CreateVersion7();
        var factory = hosts.Showcase(tenant);
        factory.BillingClient.Reset();
        return new(factory, tenant);
    }

    [Fact(DisplayName = nameof(CancelAwaitingOrderSucceedsAndEmitsOrderCancelledOutbox))]
    public async Task CancelAwaitingOrderSucceedsAndEmitsOrderCancelledOutbox()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-cancel-awaiting");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        using var client = f.Client();
        using var cancelResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);

        var cancelledBody = await OrderFixture.BodyAsync(cancelResp);
        Assert.Equal(orderId, cancelledBody.GetProperty("orderId").GetGuid());
        Assert.Equal("cancelled", cancelledBody.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, cancelledBody.GetProperty("cancelledAt").ValueKind);

        OrderHttpContract.AssertValid(cancelledBody, "StudentOrder");

        await using var scope = f.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var outboxMsg = await db.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "vendas.pedido-cancelado.v1", OrderFixture.Cancellation);
        Assert.NotNull(outboxMsg);

        using var payloadDoc = JsonDocument.Parse(outboxMsg.Payload);
        Assert.Equal(orderId, payloadDoc.RootElement.GetProperty("orderId").GetGuid());
        Assert.Equal(f.Student, payloadDoc.RootElement.GetProperty("studentId").GetGuid());
    }

    [Fact(DisplayName = nameof(CancelOrderIsIdempotentAndDoesNotEmitDuplicateOutbox))]
    public async Task CancelOrderIsIdempotentAndDoesNotEmitDuplicateOutbox()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-cancel-idempotent");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        using var client = f.Client();
        using var firstResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        using var secondResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, secondResp.StatusCode);

        var secondBody = await OrderFixture.BodyAsync(secondResp);
        Assert.Equal("cancelled", secondBody.GetProperty("status").GetString());

        await using var scope = f.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var outboxCount = await db.OutboxMessages.CountAsync(m => m.RoutingKey == "vendas.pedido-cancelado.v1", OrderFixture.Cancellation);
        Assert.Equal(1, outboxCount);
    }

    [Fact(DisplayName = nameof(CancelPaidOrderReturns422OrderNotCancellable))]
    public async Task CancelPaidOrderReturns422OrderNotCancellable()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-cancel-paid");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var now = DateTimeOffset.UtcNow;
        var paidFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "card",
            49700,
            "BRL",
            "pi_paid_card_cancel_test",
            now,
            now);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paidFact, OrderFixture.Cancellation);
        }

        using var client = f.Client();
        using var cancelResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelResp.StatusCode);

        var errorBody = await OrderFixture.BodyAsync(cancelResp);
        Assert.Equal("ORDER_NOT_CANCELLABLE", errorBody.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(CancelExpiredOrderReturns422OrderNotCancellable))]
    public async Task CancelExpiredOrderReturns422OrderNotCancellable()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-cancel-expired");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var notConfirmedFact = new PaymentNotConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "expired",
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyNotConfirmedAsync(notConfirmedFact, OrderFixture.Cancellation);
        }

        using var client = f.Client();
        using var cancelResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelResp.StatusCode);

        var errorBody = await OrderFixture.BodyAsync(cancelResp);
        Assert.Equal("ORDER_NOT_CANCELLABLE", errorBody.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(CancelOrderAsDifferentStudentReturns404NotFound))]
    public async Task CancelOrderAsDifferentStudentReturns404NotFound()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-cancel-other-student");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var otherStudent = Guid.CreateVersion7();
        using var otherClient = f.Client(student: otherStudent);
        using var cancelResp = await otherClient.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, cancelResp.StatusCode);
    }

    [Fact(DisplayName = nameof(LatePaymentConfirmedOnCancelledOrderTransitionsToPaidAndEmitsPurchaseFact))]
    public async Task LatePaymentConfirmedOnCancelledOrderTransitionsToPaidAndEmitsPurchaseFact()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-late-pay-cancelled");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        using var client = f.Client();
        using var cancelResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);

        // RN-V08: Late payment confirmed
        var confirmedAt = DateTimeOffset.UtcNow;
        var paidFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "boleto",
            49700,
            "BRL",
            "pi_late_boleto_cancelled",
            confirmedAt,
            confirmedAt);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paidFact, OrderFixture.Cancellation);
        }

        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("paid", readOrder.GetProperty("status").GetString());
        Assert.Equal("boleto", readOrder.GetProperty("paymentMethod").GetString());

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");

        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var purchaseMsg = await db.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "vendas.compra-concluida.v1", OrderFixture.Cancellation);
            Assert.NotNull(purchaseMsg);
        }
    }

    [Theory(DisplayName = nameof(StartPaymentOnACancelledOrExpiredOrderReturns422OrderNotPayable))]
    [InlineData("cancelled")]
    [InlineData("expired")]
    public async Task StartPaymentOnACancelledOrExpiredOrderReturns422OrderNotPayable(string state)
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], $"key-not-payable-{state}");
        var orderId = (await OrderFixture.BodyAsync(createResp)).GetProperty("orderId").GetGuid();
        await CloseOrderAsync(f, orderId, state);

        using var client = f.Client();
        using var payResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/payment-session", null, OrderFixture.Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, payResp.StatusCode);
        Assert.Equal("ORDER_NOT_PAYABLE", (await OrderFixture.BodyAsync(payResp)).GetProperty("code").GetString());
    }

    [Theory(DisplayName = nameof(NewPurchaseOfTheSameOfferAfterCancellingOrExpiringCreatesANewOrderAtTheCurrentPrice))]
    [InlineData("cancelled")]
    [InlineData("expired")]
    public async Task NewPurchaseOfTheSameOfferAfterCancellingOrExpiringCreatesANewOrderAtTheCurrentPrice(string state)
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var firstResp = await f.CreateAsync(ids[1], $"key-first-{state}");
        var first = await OrderFixture.BodyAsync(firstResp);
        var firstId = first.GetProperty("orderId").GetGuid();
        Assert.Equal(49700, first.GetProperty("priceCents").GetInt32());
        await CloseOrderAsync(f, firstId, state);

        // DP-04, RN-V07: the offer changed meanwhile; a new purchase is priced by the conditions in force.
        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var course = await db.CatalogCourseViews.Include(x => x.Offers).SingleAsync(x => x.CourseId == ids[0], OrderFixture.Cancellation);
            course.UpdateOffer(ids[1], new("Changed", 59700, AccessPeriod.Create("months", 6)), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(OrderFixture.Cancellation);
        }

        using var secondResp = await f.CreateAsync(ids[1], $"key-second-{state}");
        Assert.True(secondResp.IsSuccessStatusCode, $"Unexpected status {(int)secondResp.StatusCode}.");
        var second = await OrderFixture.BodyAsync(secondResp);

        Assert.NotEqual(firstId, second.GetProperty("orderId").GetGuid());
        Assert.Equal("awaiting-payment", second.GetProperty("status").GetString());
        Assert.Equal(59700, second.GetProperty("priceCents").GetInt32());
    }

    private static async Task CloseOrderAsync(OrderFixture f, Guid orderId, string state)
    {
        if (state == "cancelled")
        {
            using var client = f.Client();
            using var cancelResp = await client.PostAsync($"/internal/v1/orders/{orderId:D}/cancellation", null, OrderFixture.Cancellation);
            Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);
            return;
        }

        await using var scope = f.Scope();
        var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
        await sink.ApplyNotConfirmedAsync(
            new PaymentNotConfirmedFact(Guid.CreateVersion7(), f.Tenant, Guid.CreateVersion7(), orderId, "expired", DateTimeOffset.UtcNow), OrderFixture.Cancellation);
    }
}
