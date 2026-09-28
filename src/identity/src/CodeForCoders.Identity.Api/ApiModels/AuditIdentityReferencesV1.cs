namespace CodeForCoders.Identity.Api.ApiModels;

public sealed record AuditIdentityReferenceV1(string? Type, Guid Id);

public sealed record AuditIdentityReferenceLookupRequestV1(
    IReadOnlyList<AuditIdentityReferenceV1>? References);

public sealed record ResolvedAuditIdentityReferenceV1(
    string Type,
    Guid Id,
    string? Label);

public sealed record AuditIdentityReferenceLookupResponseV1(
    IReadOnlyList<ResolvedAuditIdentityReferenceV1> Data);
