using CodeForCoders.Media.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeForCoders.Media.Infra.Data;

public sealed class MediaUnitOfWork(MediaDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> TryCommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
