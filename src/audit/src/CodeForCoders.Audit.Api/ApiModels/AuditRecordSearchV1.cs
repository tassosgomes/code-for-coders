using System.Text.Json.Serialization;

namespace CodeForCoders.Audit.Api.ApiModels;

public sealed class AuditRecordSearchRequestV1
{
    [JsonPropertyName("_page")]
    public int Page { get; init; } = 1;

    [JsonPropertyName("_size")]
    public int Size { get; init; } = 10;

    public string? Snapshot { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public string? Type { get; init; }

    public Guid? AuthorId { get; init; }

    public Guid? TargetId { get; init; }

    public bool? Compliant { get; init; }
}

public sealed record AuditRecordIdentityReferenceV1(string Type, Guid Id);

public sealed record AuditRecordSummaryV1(
    Guid Id,
    string? Type,
    DateTimeOffset? PracticedAt,
    AuditRecordIdentityReferenceV1? Author,
    AuditRecordIdentityReferenceV1? Target,
    bool Compliant,
    bool HasComplements);

public sealed record AuditRecordPaginationV1(int Page, int Size, int Total, int TotalPages, string Snapshot);

public sealed record AuditRecordPageV1(
    IReadOnlyList<AuditRecordSummaryV1> Data,
    AuditRecordPaginationV1 Pagination);
