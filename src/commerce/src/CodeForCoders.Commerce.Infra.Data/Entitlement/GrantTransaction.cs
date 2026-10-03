using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class GrantTransaction(IDbContextTransaction transaction) : IGrantTransaction
{
    public Task CompleteAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
