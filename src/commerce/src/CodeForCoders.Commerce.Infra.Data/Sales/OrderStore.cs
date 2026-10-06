using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CodeForCoders.Commerce.Infra.Data.Sales;

public sealed class OrderStore(CommerceDbContext db) : IOrderStore
{
    public async Task<IOrderTransaction> LockAsync(OrderScope scope, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var key = $"order:{scope.TenantId:D}:{scope.StudentId:D}:{scope.KeyHash}";
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            return new OrderTransaction(transaction);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    public Task<OrderReceipt?> FindReceiptAsync(OrderScope scope, CancellationToken cancellationToken) => db.OrderReceipts
        .SingleOrDefaultAsync(item => item.StudentId == scope.StudentId && item.KeyHash == scope.KeyHash, cancellationToken);
    public Task<Order?> FindPendingAsync(Guid studentId, Guid offerId, CancellationToken cancellationToken) => db.Orders
        .SingleOrDefaultAsync(item => item.StudentId == studentId && item.OfferId == offerId && item.Status == "awaiting-payment", cancellationToken);
    public Task<Order?> FindAsync(Guid studentId, Guid orderId, CancellationToken cancellationToken) => db.Orders.AsNoTracking()
        .SingleOrDefaultAsync(item => item.StudentId == studentId && item.Id == orderId, cancellationToken);
    public async Task<long> NextNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // The tenant counter allocates a sequential number under a row lock in the creation transaction.
        await db.Database.ExecuteSqlAsync($"INSERT INTO sales.order_sequences (tenant_id, value) VALUES ({tenantId}, 0) ON CONFLICT (tenant_id) DO NOTHING", cancellationToken);
        var counter = await db.OrderSequences.FromSqlInterpolated($"SELECT * FROM sales.order_sequences WHERE tenant_id = {tenantId} FOR UPDATE").SingleAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"UPDATE sales.order_sequences SET value = value + 1 WHERE tenant_id = {tenantId}", cancellationToken);
        return counter.Value + 1;
    }
    public void DiscardChanges() => db.ChangeTracker.Clear();
    public void Add(Order order) => db.Orders.Add(order);
    public void Add(OrderReceipt receipt) { if (db.Entry(receipt).State == EntityState.Detached) db.OrderReceipts.Add(receipt); }
}
