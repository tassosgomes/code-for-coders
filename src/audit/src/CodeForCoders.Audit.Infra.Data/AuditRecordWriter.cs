using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Domain.Entities;

namespace CodeForCoders.Audit.Infra.Data;

public sealed class AuditRecordWriter(AuditDbContext dbContext) : IAuditRecordWriter
{
    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        dbContext.AuditRecords.Add(record);
        return Task.CompletedTask;
    }
}
