using System.Text.Json.Serialization;

namespace CodeForCoders.BffAdmin.Contracts;

public sealed record AuditRecordSearchRequestV1(
    [property: JsonPropertyName("_page")] int Page,
    [property: JsonPropertyName("_size")] int Size,
    string? Snapshot,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Type,
    Guid? AuthorId,
    Guid? TargetId,
    bool? Compliant);

public sealed record AuditRecordIdentityReferenceV1(string Type, Guid Id, string? Label = null);

public sealed record AuditRecordSummaryV1(
    Guid Id,
    string? Type,
    DateTimeOffset? PracticedAt,
    AuditRecordIdentityReferenceV1? Author,
    AuditRecordIdentityReferenceV1? Target,
    bool Compliant,
    bool HasComplements,
    string? Role = null);

public sealed record AuditRecordPaginationV1(int Page, int Size, int Total, int TotalPages, string Snapshot);

public sealed record AuditRecordPageV1(
    IReadOnlyList<AuditRecordSummaryV1> Data,
    AuditRecordPaginationV1 Pagination);

public sealed record AuditRecordDetailV1(
    Guid Id,
    string? Type,
    DateTimeOffset? PracticedAt,
    AuditRecordIdentityReferenceV1? Author,
    AuditRecordIdentityReferenceV1? Target,
    bool Compliant,
    bool HasComplements,
    string Origin,
    DateTimeOffset ReceivedAt,
    string? Reason,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<string> NonComplianceReasons,
    IReadOnlyList<AuditRecordComplementV1> Complements);

public sealed record AuditRecordComplementV1(
    Guid Id,
    Guid ConfirmationId,
    DateTimeOffset CreatedAt,
    AuditRecordIdentityReferenceV1? Author,
    string Explanation);

public sealed record AuditIdentityReferenceV1(string Type, Guid Id, string? Label = null);

public sealed record AuditIdentityReferenceLookupRequestV1(
    IReadOnlyList<AuditIdentityReferenceLookupItemV1> References);

public sealed record AuditIdentityReferenceLookupItemV1(string Type, Guid Id);

public sealed record AuditIdentityReferenceLookupResponseV1(
    IReadOnlyList<AuditIdentityReferenceV1> Data);
