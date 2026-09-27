using CodeForCoders.Identity.Application.Interfaces;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveAuditIdentityReferences;

public sealed record ResolveAuditIdentityReferencesInput(
    Guid TenantId,
    IReadOnlyList<AuditIdentityReferenceKey> References);
