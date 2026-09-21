using CodeForCoders.Audit.Application.Interfaces;

namespace CodeForCoders.Audit.Infra.Data;

public sealed class AuditUnitOfWork(AuditDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
