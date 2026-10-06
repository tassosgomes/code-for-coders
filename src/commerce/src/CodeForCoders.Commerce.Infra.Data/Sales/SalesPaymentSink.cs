using System.Diagnostics;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
namespace CodeForCoders.Commerce.Infra.Data.Sales;

public sealed class SalesPaymentSink(IOrderPaymentStore store, ITenantContext tenant, IOutboxMessageWriter outbox,
 IUnitOfWork unitOfWork, ILogger<SalesPaymentSink> logger, TimeProvider clock) : ISalesPaymentSink
{
    public async Task ApplyAsync(PaymentConfirmedFact fact, CancellationToken cancellationToken)
    {
        if (fact.EventId == Guid.Empty || fact.TenantId == Guid.Empty || fact.OrderId == Guid.Empty)
            throw new OrderRuleException("PAYMENT_INVALID", "Payment fact is invalid.");
        tenant.Set(fact.TenantId);
        await using var transaction = await store.LockAsync(fact.TenantId, fact.OrderId, cancellationToken);
        var order = await store.FindAsync(fact.OrderId, cancellationToken)
         ?? throw new OrderRuleException("ORDER_UNKNOWN", "Payment refers to an unknown order.");
        if (order.ConfirmPayment(new(fact.Method, fact.AmountCents, fact.Currency, fact.GatewayReference, fact.ConfirmedAt)))
        {
            if (order.PriceCents != fact.AmountCents)
            { CommerceTelemetry.PaymentAmountMismatches.Add(1); logger.LogWarning("Confirmed payment amount differs from frozen order terms."); }
            var eventId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            var purchase = new PurchaseCompletedFact(eventId, fact.TenantId, order.Id, order.Number, order.StudentId,
             order.CourseId, order.OfferId, order.PriceCents, fact.AmountCents, order.Currency,
             AccessPeriod.Create(order.PeriodType, order.PeriodMonths), fact.Method, fact.ConfirmedAt, now);
            await outbox.AppendAsync(new(eventId, fact.TenantId, "CompraConcluida", "vendas.compra-concluida.v1",
             purchase, now, Activity.Current?.Id), cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        await transaction.CompleteAsync(cancellationToken);
    }

    public async Task ApplyAwaitingAsync(PaymentAwaitingFact fact, CancellationToken cancellationToken)
    {
        if (fact.EventId == Guid.Empty || fact.TenantId == Guid.Empty || fact.OrderId == Guid.Empty)
            throw new OrderRuleException("PAYMENT_INVALID", "Payment fact is invalid.");
        tenant.Set(fact.TenantId);
        await using var transaction = await store.LockAsync(fact.TenantId, fact.OrderId, cancellationToken);
        var order = await store.FindAsync(fact.OrderId, cancellationToken)
         ?? throw new OrderRuleException("ORDER_UNKNOWN", "Payment refers to an unknown order.");
        if (order.RecordPendingPayment(fact.Method, fact.GatewayReference, fact.ExpiresAt))
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        await transaction.CompleteAsync(cancellationToken);
    }

    public async Task ApplyNotConfirmedAsync(PaymentNotConfirmedFact fact, CancellationToken cancellationToken)
    {
        if (fact.EventId == Guid.Empty || fact.TenantId == Guid.Empty || fact.OrderId == Guid.Empty)
            throw new OrderRuleException("PAYMENT_INVALID", "Payment fact is invalid.");
        tenant.Set(fact.TenantId);
        await using var transaction = await store.LockAsync(fact.TenantId, fact.OrderId, cancellationToken);
        var order = await store.FindAsync(fact.OrderId, cancellationToken)
         ?? throw new OrderRuleException("ORDER_UNKNOWN", "Payment refers to an unknown order.");

        if (fact.Reason == "expired" && order.Expire(fact.OccurredAt))
        {
            var eventId = Guid.CreateVersion7();
            var payload = new
            {
                eventId,
                order.TenantId,
                orderId = order.Id,
                order.StudentId,
                occurredAt = fact.OccurredAt
            };
            await outbox.AppendAsync(new(eventId, order.TenantId, "PedidoExpirado", "vendas.pedido-expirado.v1",
                payload, fact.OccurredAt, Activity.Current?.Id), cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        await transaction.CompleteAsync(cancellationToken);
    }
}
