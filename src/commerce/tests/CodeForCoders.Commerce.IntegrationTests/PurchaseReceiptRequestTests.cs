using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Infra.Messaging;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class PurchaseReceiptRequestTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private OrderFixture Fixture()
    {
        var tenant = Guid.CreateVersion7();
        return new(hosts.Showcase(tenant), tenant);
    }

    [Fact(DisplayName = nameof(PaidOrderWritesFrozenReceiptAtomicallyWithoutPersonalContact))]
    public async Task PaidOrderWritesFrozenReceiptAtomicallyWithoutPersonalContact()
    {
        var f = Fixture();
        var (orderId, payment) = await OrderAsync(f);
        await using var scope = f.Scope();
        await scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>().ApplyAsync(payment, Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId, Cancellation);
        var receipt = Assert.Single(await db.OutboxMessages.Where(m => m.RoutingKey == "notificacao.envio-solicitado.v1").ToListAsync(Cancellation));
        CommerceMessages.AssertSends(receipt.RoutingKey, receipt.Payload);
        using var json = JsonDocument.Parse(receipt.Payload);
        var root = json.RootElement;
        Assert.False(root.TryGetProperty("destinatario", out _));
        Assert.Equal(f.Student, root.GetProperty("destinatarioConta").GetProperty("id").GetGuid());
        Assert.Equal(orderId, root.GetProperty("pedidoId").GetGuid());
        var data = root.GetProperty("dados");
        Assert.Equal(order.Number, data.GetProperty("numeroPedido").GetString());
        Assert.Equal(order.CourseTitle, data.GetProperty("curso").GetString());
        Assert.Equal(order.OfferName, data.GetProperty("opcao").GetString());
        Assert.Equal(payment.AmountCents, data.GetProperty("valorCentavos").GetInt32());
        Assert.Equal($"http://localhost:8082/student/pedidos/{orderId:D}", data.GetProperty("link").GetString());
        Assert.Equal("paid", order.Status);
        Assert.Single(await db.OutboxMessages.Where(m => m.RoutingKey == "vendas.compra-concluida.v1").ToListAsync(Cancellation));
        Assert.DoesNotContain("@", receipt.Payload);
        Assert.DoesNotContain("email", JsonSerializer.Serialize(order), StringComparison.OrdinalIgnoreCase);
        Assert.All(await db.OutboxMessages.ToListAsync(Cancellation), message => Assert.DoesNotContain("@", message.Payload));
    }

    [Fact(DisplayName = nameof(RepublishedPaymentKeepsOneReceiptAndStableRequestId))]
    public async Task RepublishedPaymentKeepsOneReceiptAndStableRequestId()
    {
        var f = Fixture();
        var (orderId, payment) = await OrderAsync(f);
        await using var scope = f.Scope();
        var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
        await sink.ApplyAsync(payment, Cancellation);
        await sink.ApplyAsync(payment with { EventId = Guid.CreateVersion7() }, Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var receipt = Assert.Single(await db.OutboxMessages.Where(m => m.RoutingKey == "notificacao.envio-solicitado.v1").ToListAsync(Cancellation));
        using var json = JsonDocument.Parse(receipt.Payload);
        Assert.Equal(orderId, json.RootElement.GetProperty("pedidoId").GetGuid());
    }

    [Fact(DisplayName = nameof(LatePaymentWritesReceiptWithLifetimeAndConfirmedAmount))]
    public async Task LatePaymentWritesReceiptWithLifetimeAndConfirmedAmount()
    {
        var f = Fixture();
        var (orderId, payment) = await OrderAsync(f, lifetime: true);
        await using var scope = f.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId, Cancellation);
        order.Cancel(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Cancellation);
        await scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>().ApplyAsync(payment with { Method = "boleto", AmountCents = 80000 }, Cancellation);
        var receipt = Assert.Single(await db.OutboxMessages.Where(m => m.RoutingKey == "notificacao.envio-solicitado.v1").ToListAsync(Cancellation));
        CommerceMessages.AssertSends(receipt.RoutingKey, receipt.Payload);
        using var json = JsonDocument.Parse(receipt.Payload);
        var data = json.RootElement.GetProperty("dados");
        Assert.Equal("lifetime", data.GetProperty("vigencia").GetProperty("type").GetString());
        Assert.False(data.GetProperty("vigencia").TryGetProperty("months", out _));
        Assert.Equal("boleto", data.GetProperty("meio").GetString());
        Assert.Equal(80000, data.GetProperty("valorCentavos").GetInt32());
    }

    [Fact(DisplayName = nameof(PublisherRoutesReceiptToNotificationAndAuditToAuditExchange))]
    public async Task PublisherRoutesReceiptToNotificationAndAuditToAuditExchange()
    {
        var f = Fixture();
        var (_, payment) = await OrderAsync(f);
        OutboxMessage receipt;
        await using (var scope = f.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>().ApplyAsync(payment, Cancellation);
            receipt = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().OutboxMessages.SingleAsync(m => m.RoutingKey == "notificacao.envio-solicitado.v1", Cancellation);
        }
        var factory = hosts.Catalog;
        var options = factory.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        var notificationExchange = "receipt.notification." + Guid.CreateVersion7().ToString("N");
        var auditExchange = "receipt.audit." + Guid.CreateVersion7().ToString("N");
        options.RoutingExchanges["notificacao.envio-solicitado.v1"] = notificationExchange;
        options.RoutingExchanges["auditoria.ato-praticado.v1"] = auditExchange;
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        foreach (var (exchange, message) in new[] { (notificationExchange, receipt), (auditExchange, Audit(receipt.TenantId)) })
        {
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, true, false, cancellationToken: Cancellation);
            var queue = await channel.QueueDeclareAsync("", false, true, true, cancellationToken: Cancellation);
            await channel.QueueBindAsync(queue.QueueName, exchange, message.RoutingKey, cancellationToken: Cancellation);
            await factory.Services.GetRequiredService<RabbitMqPublisher>().PublishAsync(message, Cancellation);
            var delivery = await channel.BasicGetAsync(queue.QueueName, true, Cancellation);
            Assert.NotNull(delivery);
            Assert.Equal(exchange, delivery.Exchange);
            Assert.Equal(message.Payload, Encoding.UTF8.GetString(delivery.Body.Span));
        }
    }

    private static OutboxMessage Audit(Guid tenant) => OutboxMessage.Create(new(Guid.CreateVersion7(), tenant,
        "AuditProof", "auditoria.ato-praticado.v1", new { }, DateTimeOffset.UtcNow, null), "{}");

    private static async Task<(Guid orderId, PaymentConfirmedFact fact)> OrderAsync(OrderFixture f, bool lifetime = false)
    {
        var ids = await f.SeedAsync();
        using var response = await f.CreateAsync(ids[lifetime ? 2 : 1], Guid.CreateVersion7().ToString());
        var body = await OrderFixture.BodyAsync(response);
        var order = body.GetProperty("orderId").GetGuid();
        var now = DateTimeOffset.UtcNow;
        return (order, new(Guid.CreateVersion7(), f.Tenant, Guid.CreateVersion7(), order, "card",
            lifetime ? 89700 : 49700, "BRL", "pi_receipt", now, now));
    }
}
