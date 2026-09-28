namespace CodeForCoders.Audit.Application.UseCases.Audit.GetAuditRecord;

public sealed record AuditRecordDetailOutput(
    Guid Id,
    string? Type,
    DateTimeOffset? PracticedAt,
    AuditRecordDetailIdentityReferenceOutput? Author,
    AuditRecordDetailIdentityReferenceOutput? Target,
    bool Compliant,
    bool HasComplements,
    string Origin,
    DateTimeOffset ReceivedAt,
    string? Reason,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<string> NonComplianceReasons,
    IReadOnlyList<AuditRecordComplementOutput> Complements);

public sealed record AuditRecordDetailIdentityReferenceOutput(string Type, Guid Id);

public sealed record AuditRecordComplementOutput(
    Guid Id,
    Guid ConfirmationId,
    DateTimeOffset CreatedAt,
    AuditRecordDetailIdentityReferenceOutput? Author,
    string Explanation);
