using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Infra.Data;

public sealed class LearningUnitOfWork(LearningDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
