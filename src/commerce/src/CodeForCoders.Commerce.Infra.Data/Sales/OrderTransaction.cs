using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
namespace CodeForCoders.Commerce.Infra.Data.Sales;

public sealed class OrderTransaction(IDbContextTransaction transaction) : IOrderTransaction
{
    public Task CompleteAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
