namespace CodeForCoders.Audit.Application.UseCases.Audit.SearchAuditRecords;

public sealed record SearchAuditRecordsInput(
    Guid TenantId,
    Guid SessionId,
    int Page,
    int Size,
    string? Snapshot,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Type,
    Guid? AuthorId,
    Guid? TargetId,
    bool? Compliant);
