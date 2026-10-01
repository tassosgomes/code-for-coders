using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeForCoders.Commerce.Infra.Data.Catalog;

public sealed class CatalogEditTransaction(IDbContextTransaction transaction) : ICatalogEditTransaction
{
    public Task CompleteAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
