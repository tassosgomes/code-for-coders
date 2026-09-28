using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Audit.Infra.Data;

public sealed class AuditRecordWriter(AuditDbContext dbContext) : IAuditRecordWriter
{
    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        dbContext.AuditRecords.Add(record);
        return Task.CompletedTask;
    }

    public Task<string?> ReadFingerprintAsync(
        string origin,
        Guid factId,
        CancellationToken cancellationToken)
        => dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.Origin == origin && record.FactId == factId)
            .Select(record => record.Fingerprint)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<AuditRecord?> FindOriginalAsync(
        Guid tenantId,
        Guid recordId,
        CancellationToken cancellationToken)
        => dbContext.AuditRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.TenantId == tenantId
                && record.Id == recordId
                && record.RecordType == AuditRecord.OriginalRecordType,
                cancellationToken);

    public Task<AuditRecord?> FindByConfirmationIdAsync(
        Guid tenantId,
        Guid confirmationId,
        CancellationToken cancellationToken)
        => dbContext.AuditRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.TenantId == tenantId
                && record.ConfirmationId == confirmationId,
                cancellationToken);
}
