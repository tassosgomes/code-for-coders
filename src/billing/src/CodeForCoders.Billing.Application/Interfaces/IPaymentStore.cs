using CodeForCoders.Billing.Domain.Entities;
namespace CodeForCoders.Billing.Application.Interfaces;

public interface IPaymentStore
{
    Task<IPaymentTransaction> LockAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken);
    Task<Payment?> FindAsync(Guid orderId, CancellationToken cancellationToken);
    void Add(Payment payment);
    Task ReceiveAsync(GatewayEvent receipt, CancellationToken cancellationToken);
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken);
}
