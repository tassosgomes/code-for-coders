using CodeForCoders.BffStudent.Application.Interfaces;

namespace CodeForCoders.BffStudent.Infra.Data;

public sealed class BffStudentUnitOfWork(BffStudentDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
