using CodeForCoders.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Billing.Infra.Data;

public sealed class BillingUnitOfWork(BillingDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
