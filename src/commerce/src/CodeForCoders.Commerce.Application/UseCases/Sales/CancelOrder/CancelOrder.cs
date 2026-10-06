using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.Sales.Common;
using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Application.UseCases.Sales.CancelOrder;

public sealed class CancelOrder(
    IOrderPaymentStore store,
    IUnitOfWork unitOfWork,
    IOutboxMessageWriter outbox,
    ITenantContext tenant,
    TimeProvider clock) : ICancelOrder
{
    public async Task<StudentOrder> ExecuteAsync(CancelOrderInput input, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId ?? throw new OrderRuleException("INVALID_REQUEST", "Tenant is required.");
        await using var transaction = await store.LockAsync(tenantId, input.OrderId, cancellationToken);
        var order = await store.FindAsync(input.OrderId, cancellationToken);
        if (order is null || order.StudentId != input.StudentId)
            throw new NotFoundException("ORDER_NOT_FOUND");

        if (order.Status == "cancelled")
        {
            await transaction.CompleteAsync(cancellationToken);
            return StudentOrder.FromOrder(order);
        }

        var now = clock.GetUtcNow();
        order.Cancel(now);

        var eventId = Guid.CreateVersion7();
        var payload = new
        {
            eventId,
            order.TenantId,
            orderId = order.Id,
            order.StudentId,
            occurredAt = now
        };
        await outbox.AppendAsync(new(eventId, order.TenantId, "PedidoCancelado", "vendas.pedido-cancelado.v1", payload, now, input.TraceParent), cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return StudentOrder.FromOrder(order);
    }
}
