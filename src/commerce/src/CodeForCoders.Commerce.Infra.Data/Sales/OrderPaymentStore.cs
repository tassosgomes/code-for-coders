using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace CodeForCoders.Commerce.Infra.Data.Sales;

public sealed class OrderPaymentStore(CommerceDbContext db) : IOrderPaymentStore
{
    public async Task<IOrderTransaction> LockAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var key = $"order-payment/{tenantId:D}/{orderId:D}";
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            return new OrderTransaction(transaction);
        }
        catch (OperationCanceledException) { await transaction.DisposeAsync(); throw; }
        catch (NpgsqlException) { await transaction.DisposeAsync(); throw; }
    }
    public Task<Order?> FindAsync(Guid orderId, CancellationToken cancellationToken) => db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
}
