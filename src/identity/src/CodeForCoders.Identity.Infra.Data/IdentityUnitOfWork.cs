using CodeForCoders.Identity.Application.Interfaces;

namespace CodeForCoders.Identity.Infra.Data;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
