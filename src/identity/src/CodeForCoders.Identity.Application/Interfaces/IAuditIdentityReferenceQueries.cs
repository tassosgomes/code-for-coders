namespace CodeForCoders.Identity.Application.Interfaces;

public interface IAuditIdentityReferenceQueries
{
    Task<IReadOnlyDictionary<AuditIdentityReferenceKey, string>> ResolveLabelsAsync(
        Guid tenantId,
        IReadOnlyList<AuditIdentityReferenceKey> references,
        CancellationToken cancellationToken);
}

public sealed record AuditIdentityReferenceKey(string Type, Guid Id);
