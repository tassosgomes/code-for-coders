using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeForCoders.Identity.Infra.Data;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (TryGetRegistrationConstraint(exception, out var constraintName))
        {
            dbContext.ChangeTracker.Clear();
            throw new RegistrationWriteConflictException(constraintName, exception);
        }
    }

    private static bool TryGetRegistrationConstraint(Exception exception, out string constraintName)
    {
        if (exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName is "ux_accounts_tenant_id_normalized_email"
                or "ux_idempotency_records_scope_key")
        {
            constraintName = postgresException.ConstraintName;
            return true;
        }

        constraintName = string.Empty;
        return false;
    }
}
