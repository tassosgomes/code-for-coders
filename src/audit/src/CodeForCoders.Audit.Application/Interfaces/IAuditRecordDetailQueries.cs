using CodeForCoders.Audit.Domain.Entities;

namespace CodeForCoders.Audit.Application.Interfaces;

public interface IAuditRecordDetailQueries
{
    Task<AuditRecordDetailData?> FindOriginalWithComplementsAsync(
        Guid tenantId,
        Guid recordId,
        CancellationToken cancellationToken);
}

public sealed record AuditRecordDetailData(
    AuditRecord Original,
    IReadOnlyList<AuditRecord> Complements);
