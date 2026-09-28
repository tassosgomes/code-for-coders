namespace CodeForCoders.Audit.Application.UseCases.Audit.SearchAuditRecords;

public sealed record SearchAuditRecordsOutput(
    IReadOnlyList<AuditRecordSummaryOutput> Data,
    AuditRecordPaginationOutput Pagination);

public sealed record AuditRecordIdentityReferenceOutput(string Type, Guid Id, string? Label = null);

public sealed record AuditRecordSummaryOutput(
    Guid Id,
    string? Type,
    DateTimeOffset? PracticedAt,
    AuditRecordIdentityReferenceOutput? Author,
    AuditRecordIdentityReferenceOutput? Target,
    bool Compliant,
    bool HasComplements,
    string? Role = null);

public sealed record AuditRecordPaginationOutput(int Page, int Size, int Total, int TotalPages, string Snapshot);
