using CodeForCoders.Audit.Domain.Entities;

namespace CodeForCoders.Audit.Application.Interfaces;

public interface IAuditRecordSearchQueries
{
    Task<IReadOnlyList<Guid>> SelectOriginalIdsAsync(
        Guid tenantId,
        AuditRecordSearchFilters filters,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditRecord>> FindOriginalsByIdsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken);
}

public sealed record AuditRecordSearchFilters(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Type,
    Guid? AuthorId,
    Guid? TargetId,
    bool? Compliant);

public sealed record AuditRecordSnapshot(
    Guid TenantId,
    Guid SessionId,
    AuditRecordSearchFilters Filters,
    int Size,
    IReadOnlyList<Guid> RecordIds);

public interface IAuditRecordSnapshotStore
{
    Task<bool> TryCreateAsync(
        string snapshotId,
        AuditRecordSnapshot snapshot,
        TimeSpan timeToLive,
        CancellationToken cancellationToken);

    Task<AuditRecordSnapshot?> FindAsync(string snapshotId, CancellationToken cancellationToken);
}
