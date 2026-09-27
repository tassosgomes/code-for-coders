using CodeForCoders.Audit.Domain.Exceptions;
using CodeForCoders.Audit.Infra.Data.Configuration;
using CodeForCoders.Audit.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeForCoders.Audit.Infra.Data;

public sealed class AuditUnitOfWork(AuditDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAuditRecordKey(exception))
        {
            throw new AuditRecordAlreadyExistsException();
        }
    }

    private static bool IsDuplicateAuditRecordKey(DbUpdateException exception)
        => exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AuditRecordConfiguration.UniqueOriginFactIdIndexName
                or "ux_audit_records_tenant_confirmation_id",
        };
}
