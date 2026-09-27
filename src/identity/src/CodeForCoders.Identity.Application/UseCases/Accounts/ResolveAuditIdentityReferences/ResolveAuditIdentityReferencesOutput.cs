namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveAuditIdentityReferences;

public sealed record ResolveAuditIdentityReferencesOutput(
    IReadOnlyList<ResolvedAuditIdentityReference> Data);

public sealed record ResolvedAuditIdentityReference(
    string Type,
    Guid Id,
    string? Label);
