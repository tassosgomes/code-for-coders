using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Audit.Infra.Data.Queries;

public sealed class AuditRecordDetailQueries(AuditDbContext dbContext) : IAuditRecordDetailQueries
{
    public async Task<AuditRecordDetailData?> FindOriginalWithComplementsAsync(
        Guid tenantId,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var original = await dbContext.AuditRecords.AsNoTracking()
            .FirstOrDefaultAsync(record => record.TenantId == tenantId
                && record.Id == recordId
                && record.RecordType == AuditRecord.OriginalRecordType,
                cancellationToken);
        if (original is null)
        {
            return null;
        }

        var complements = await dbContext.AuditRecords.AsNoTracking()
            .Where(record => record.TenantId == tenantId
                && record.OriginalRecordId == original.Id
                && record.RecordType == AuditRecord.ComplementRecordType)
            .OrderBy(record => record.ConfirmedAt)
            .ThenBy(record => record.Id)
            .ToListAsync(cancellationToken);

        return new AuditRecordDetailData(original, complements);
    }
}
