using CodeForCoders.Commerce.Domain.Entities;
namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IOrderStore
{
    Task<IOrderTransaction> LockAsync(OrderScope scope, CancellationToken cancellationToken);
    Task<OrderReceipt?> FindReceiptAsync(OrderScope scope, CancellationToken cancellationToken);
    Task<Order?> FindPendingAsync(Guid studentId, Guid offerId, CancellationToken cancellationToken);
    Task<Order?> FindAsync(Guid studentId, Guid orderId, CancellationToken cancellationToken);
    Task<long> NextNumberAsync(Guid tenantId, CancellationToken cancellationToken);
    void DiscardChanges();
    void Add(Order order);
    void Add(OrderReceipt receipt);
}
