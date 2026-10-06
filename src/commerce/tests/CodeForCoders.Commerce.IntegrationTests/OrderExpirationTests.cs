using System.Net;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OrderExpirationTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private OrderFixture Fixture()
    {
        var tenant = Guid.CreateVersion7();
        var factory = hosts.Showcase(tenant);
        factory.BillingClient.Reset();
        return new(factory, tenant);
    }

    [Fact(DisplayName = nameof(ReceivingPaymentNotConfirmedExpiredFactTransitionsOrderToExpiredAndEmitsOrderExpiredOutbox))]
    public async Task ReceivingPaymentNotConfirmedExpiredFactTransitionsOrderToExpiredAndEmitsOrderExpiredOutbox()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-expire-not-confirmed");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var occurredAt = DateTimeOffset.UtcNow;
        var notConfirmedFact = new PaymentNotConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "expired",
            occurredAt);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyNotConfirmedAsync(notConfirmedFact, OrderFixture.Cancellation);
        }

        using var client = f.Client();
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("expired", readOrder.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, readOrder.GetProperty("expiredAt").ValueKind);

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");

        await using var scopeCheck = f.Scope();
        var db = scopeCheck.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var outboxMsg = await db.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "vendas.pedido-expirado.v1", OrderFixture.Cancellation);
        Assert.NotNull(outboxMsg);

        using var payloadDoc = JsonDocument.Parse(outboxMsg.Payload);
        Assert.Equal(orderId, payloadDoc.RootElement.GetProperty("orderId").GetGuid());
        Assert.Equal(f.Student, payloadDoc.RootElement.GetProperty("studentId").GetGuid());
    }

    [Fact(DisplayName = nameof(ReceivingPaymentNotConfirmedCancelledFactDoesNotExpireOrder))]
    public async Task ReceivingPaymentNotConfirmedCancelledFactDoesNotExpireOrder()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-not-confirmed-cancelled");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        var notConfirmedFact = new PaymentNotConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "cancelled",
            DateTimeOffset.UtcNow);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyNotConfirmedAsync(notConfirmedFact, OrderFixture.Cancellation);
        }

        using var client = f.Client();
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("awaiting-payment", readOrder.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, readOrder.GetProperty("expiredAt").ValueKind);

        await using var scopeCheck = f.Scope();
        var db = scopeCheck.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var outboxCount = await db.OutboxMessages.CountAsync(m => m.RoutingKey == "vendas.pedido-expirado.v1", OrderFixture.Cancellation);
        Assert.Equal(0, outboxCount);
    }

    [Fact(DisplayName = nameof(ReceivingPaymentNotConfirmedFactOnAlreadyPaidOrderHasNoEffect))]
    public async Task ReceivingPaymentNotConfirmedFactOnAlreadyPaidOrderHasNoEffect()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-paid-before-expired");
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
            "pi_paid_card_expire_test",
            confirmedAt,
            confirmedAt);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paidFact, OrderFixture.Cancellation);
        }

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
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("paid", readOrder.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, readOrder.GetProperty("expiredAt").ValueKind);
    }

    [Fact(DisplayName = nameof(OrderExpirationCycleExpiresOrderCreatedMoreThan24HoursAgoWithoutPaymentPage))]
    public async Task OrderExpirationCycleExpiresOrderCreatedMoreThan24HoursAgoWithoutPaymentPage()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-expire-routine-created");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        // Update created_at in the database to 25 hours ago
        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var oldDate = DateTimeOffset.UtcNow.AddHours(-25);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.orders SET created_at = {oldDate} WHERE id = {orderId}", OrderFixture.Cancellation);
        }

        // Run expiration cycle
        await using (var scope = f.Scope())
        {
            var cycle = scope.ServiceProvider.GetRequiredService<OrderExpirationCycle>();
            var expiredCount = await cycle.RunAsync(OrderFixture.Cancellation);
            Assert.True(expiredCount >= 1);
        }

        using var client = f.Client();
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("expired", readOrder.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, readOrder.GetProperty("expiredAt").ValueKind);

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");

        await using var scopeCheck = f.Scope();
        var dbCheck = scopeCheck.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var outboxMsg = await dbCheck.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "vendas.pedido-expirado.v1", OrderFixture.Cancellation);
        Assert.NotNull(outboxMsg);
    }

    [Fact(DisplayName = nameof(OrderExpirationCycleExpiresOrderWithPendingPaymentOverdueMoreThan24Hours))]
    public async Task OrderExpirationCycleExpiresOrderWithPendingPaymentOverdueMoreThan24Hours()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-expire-routine-pending");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();

        // Simulate boleto expired 25 hours ago
        var overduePending = DateTimeOffset.UtcNow.AddHours(-25);
        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE sales.orders SET pending_payment_expires_at = {overduePending}, payment_method = 'boleto' WHERE id = {orderId}", OrderFixture.Cancellation);
        }

        // Run expiration cycle
        await using (var scope = f.Scope())
        {
            var cycle = scope.ServiceProvider.GetRequiredService<OrderExpirationCycle>();
            var expiredCount = await cycle.RunAsync(OrderFixture.Cancellation);
            Assert.True(expiredCount >= 1);
        }

        using var client = f.Client();
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("expired", readOrder.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, readOrder.GetProperty("expiredAt").ValueKind);
    }

    [Fact(DisplayName = nameof(LatePaymentConfirmedOnExpiredOrderTransitionsToPaidAndEmitsPurchaseFact))]
    public async Task LatePaymentConfirmedOnExpiredOrderTransitionsToPaidAndEmitsPurchaseFact()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-late-pay-expired");
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

        // RN-V08: Late payment arrives
        var confirmedAt = DateTimeOffset.UtcNow;
        var paidFact = new PaymentConfirmedFact(
            Guid.CreateVersion7(),
            f.Tenant,
            Guid.CreateVersion7(),
            orderId,
            "pix",
            49700,
            "BRL",
            "pi_late_pix_expired",
            confirmedAt,
            confirmedAt);

        await using (var scope = f.Scope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await sink.ApplyAsync(paidFact, OrderFixture.Cancellation);
        }

        using var client = f.Client();
        using var getResp = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var readOrder = await OrderFixture.BodyAsync(getResp);

        Assert.Equal("paid", readOrder.GetProperty("status").GetString());
        Assert.Equal("pix", readOrder.GetProperty("paymentMethod").GetString());

        OrderHttpContract.AssertValid(readOrder, "StudentOrder");

        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var purchaseMsg = await db.OutboxMessages.SingleOrDefaultAsync(m => m.RoutingKey == "vendas.compra-concluida.v1", OrderFixture.Cancellation);
            Assert.NotNull(purchaseMsg);
        }
    }

    [Theory(DisplayName = nameof(OrderExpirationCycleHonoursThe24HourBoundaryOfAKnownPaymentPageDeadline))]
    [InlineData(1, false)]
    [InlineData(-23, false)]
    [InlineData(-25, true)]
    public async Task OrderExpirationCycleHonoursThe24HourBoundaryOfAKnownPaymentPageDeadline(int pageDeadlineHoursFromNow, bool expected)
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], $"key-boundary-{pageDeadlineHoursFromNow}");
        var orderId = (await OrderFixture.BodyAsync(createResp)).GetProperty("orderId").GetGuid();

        // created_at is far in the past so only the known page deadline can decide.
        var created = DateTimeOffset.UtcNow.AddHours(-72);
        var deadline = DateTimeOffset.UtcNow.AddHours(pageDeadlineHoursFromNow);
        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE sales.orders SET created_at = {created}, payment_page_expires_at = {deadline} WHERE id = {orderId}", OrderFixture.Cancellation);
        }

        await RunCycleAsync(f);

        Assert.Equal(expected ? "expired" : "awaiting-payment", await StatusAsync(f, orderId));
        Assert.Equal(expected ? 1 : 0, (await ExpiredFactsAsync(f, orderId)).Count);
    }

    [Fact(DisplayName = nameof(OrderExpirationCycleDoesNotExpireAnOrderCreated23HoursAgoWithoutPaymentPage))]
    public async Task OrderExpirationCycleDoesNotExpireAnOrderCreated23HoursAgoWithoutPaymentPage()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-created-23h");
        var orderId = (await OrderFixture.BodyAsync(createResp)).GetProperty("orderId").GetGuid();

        var created = DateTimeOffset.UtcNow.AddHours(-23);
        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.orders SET created_at = {created} WHERE id = {orderId}", OrderFixture.Cancellation);
        }

        await RunCycleAsync(f);

        Assert.Equal("awaiting-payment", await StatusAsync(f, orderId));
        Assert.Empty(await ExpiredFactsAsync(f, orderId));
    }

    [Fact(DisplayName = nameof(OrderExpirationCycleRunTwiceEmitsASingleOrderExpiredFact))]
    public async Task OrderExpirationCycleRunTwiceEmitsASingleOrderExpiredFact()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], "key-cycle-twice");
        var orderId = (await OrderFixture.BodyAsync(createResp)).GetProperty("orderId").GetGuid();
        await AgeOrderAsync(f, orderId);

        await RunCycleAsync(f);
        await RunCycleAsync(f);

        Assert.Equal("expired", await StatusAsync(f, orderId));
        Assert.Single(await ExpiredFactsAsync(f, orderId));
    }

    [Theory(DisplayName = nameof(CycleAndBillingFactOnTheSameOrderEmitASingleOrderExpiredFact))]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CycleAndBillingFactOnTheSameOrderEmitASingleOrderExpiredFact(bool billingFirst)
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var createResp = await f.CreateAsync(ids[1], $"key-cycle-and-fact-{billingFirst}");
        var orderId = (await OrderFixture.BodyAsync(createResp)).GetProperty("orderId").GetGuid();
        await AgeOrderAsync(f, orderId);

        if (billingFirst)
        {
            await ApplyExpiredFactAsync(f, orderId);
            await RunCycleAsync(f);
        }
        else
        {
            await RunCycleAsync(f);
            await ApplyExpiredFactAsync(f, orderId);
        }

        Assert.Equal("expired", await StatusAsync(f, orderId));
        Assert.Single(await ExpiredFactsAsync(f, orderId));
    }

    private static async Task AgeOrderAsync(OrderFixture f, Guid orderId)
    {
        await using var scope = f.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var oldDate = DateTimeOffset.UtcNow.AddHours(-25);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.orders SET created_at = {oldDate} WHERE id = {orderId}", OrderFixture.Cancellation);
    }

    private static async Task RunCycleAsync(OrderFixture f)
    {
        await using var scope = f.Scope();
        await scope.ServiceProvider.GetRequiredService<OrderExpirationCycle>().RunAsync(OrderFixture.Cancellation);
    }

    private static async Task ApplyExpiredFactAsync(OrderFixture f, Guid orderId)
    {
        await using var scope = f.Scope();
        var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
        await sink.ApplyNotConfirmedAsync(
            new PaymentNotConfirmedFact(Guid.CreateVersion7(), f.Tenant, Guid.CreateVersion7(), orderId, "expired", DateTimeOffset.UtcNow), OrderFixture.Cancellation);
    }

    private static async Task<string> StatusAsync(OrderFixture f, Guid orderId)
    {
        using var client = f.Client();
        using var response = await client.GetAsync($"/internal/v1/orders/{orderId:D}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await OrderFixture.BodyAsync(response)).GetProperty("status").GetString()!;
    }

    private static async Task<List<string>> ExpiredFactsAsync(OrderFixture f, Guid orderId)
    {
        await using var scope = f.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var messages = await db.OutboxMessages.Where(m => m.RoutingKey == "vendas.pedido-expirado.v1").ToListAsync(OrderFixture.Cancellation);
        return messages.Select(m => m.Payload).Where(p => JsonDocument.Parse(p).RootElement.GetProperty("orderId").GetGuid() == orderId).ToList();
    }
}
