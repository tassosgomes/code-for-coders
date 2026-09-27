using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IAuditIdentityReferenceClient
{
    Task<AuditIdentityReferenceClientResult> ResolveAsync(
        Guid staffSessionId,
        IReadOnlyList<AuditIdentityReferenceV1> references,
        CancellationToken cancellationToken);
}

public sealed record AuditIdentityReferenceClientResult(
    int StatusCode,
    string? Code,
    IReadOnlyList<AuditIdentityReferenceV1>? Data);
