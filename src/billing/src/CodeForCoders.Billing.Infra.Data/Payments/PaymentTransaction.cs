using CodeForCoders.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
namespace CodeForCoders.Billing.Infra.Data.Payments;

public sealed class PaymentTransaction(IDbContextTransaction transaction) : IPaymentTransaction
{
    public Task CompleteAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
