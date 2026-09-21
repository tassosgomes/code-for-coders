using CodeForCoders.Media.Application.Interfaces;

namespace CodeForCoders.Media.Infra.Data;

public sealed class MediaUnitOfWork(MediaDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
