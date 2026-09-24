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
}
