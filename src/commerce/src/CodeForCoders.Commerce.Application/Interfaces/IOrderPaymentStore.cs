using CodeForCoders.Commerce.Domain.Entities;
namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IOrderPaymentStore
{
    Task<IOrderTransaction> LockAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken);
    Task<Order?> FindAsync(Guid orderId, CancellationToken cancellationToken);
}
