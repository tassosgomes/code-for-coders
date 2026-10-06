using System.Diagnostics;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.Infra.Data.Sales;

public sealed class OrderExpirationCycle(
    CommerceDbContext db,
    IOutboxMessageWriter outbox,
    TimeProvider clock,
    IOptions<OrderExpirationOptions> options,
    ILogger<OrderExpirationCycle> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var threshold = now.AddHours(-24);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var orders = await db.Orders.FromSql($"""
            SELECT * FROM sales.orders
            WHERE status = 'awaiting-payment'
              AND (
                (payment_page_expires_at IS NULL AND pending_payment_expires_at IS NULL AND created_at <= {threshold})
                OR (pending_payment_expires_at IS NOT NULL AND pending_payment_expires_at <= {threshold})
                OR (payment_page_expires_at IS NOT NULL AND pending_payment_expires_at IS NULL AND payment_page_expires_at <= {threshold})
              )
            ORDER BY created_at, id
            LIMIT {options.Value.BatchSize}
            FOR UPDATE SKIP LOCKED
            """).IgnoreQueryFilters().ToListAsync(cancellationToken);

        var correlationId = Activity.Current?.Id ?? $"order-expiration-{Guid.CreateVersion7():D}";

        foreach (var order in orders)
        {
            if (order.Expire(now))
            {
                var eventId = Guid.CreateVersion7();
                var fact = new
                {
                    eventId,
                    order.TenantId,
                    orderId = order.Id,
                    order.StudentId,
                    occurredAt = now
                };
                await outbox.AppendAsync(new(eventId, order.TenantId, "PedidoExpirado", "vendas.pedido-expirado.v1",
                    fact, now, correlationId), cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (orders.Count > 0)
        {
            logger.LogInformation("Expired {Count} overdue awaiting-payment orders.", orders.Count);
        }

        return orders.Count;
    }
}
