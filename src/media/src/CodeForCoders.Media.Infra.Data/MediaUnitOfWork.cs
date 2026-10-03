using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeForCoders.Media.Infra.Data;

public sealed class MediaUnitOfWork(MediaDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new ConcurrencyConflictException();
        }
    }

    public async Task<bool> TryCommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CommitAsync(cancellationToken);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateException exception)
            when ((exception.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.UniqueViolation
                || (exception.GetBaseException() as PostgresException)?.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
