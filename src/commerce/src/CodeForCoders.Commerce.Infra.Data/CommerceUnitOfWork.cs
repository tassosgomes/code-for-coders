using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace CodeForCoders.Commerce.Infra.Data;

public sealed class CommerceUnitOfWork(CommerceDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_orders_pending" })
        { throw new PendingOrderConflictException(exception); }
    }
}
