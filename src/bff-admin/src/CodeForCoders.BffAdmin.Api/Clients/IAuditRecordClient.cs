using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IAuditRecordClient
{
    Task<AuditRecordClientResult> SearchAsync(
        AuditRecordSearchRequestV1 request,
        string accessToken,
        CancellationToken cancellationToken);

    Task<AuditRecordDetailClientResult> GetAsync(
        Guid recordId,
        string accessToken,
        CancellationToken cancellationToken);
}

public sealed record AuditRecordClientResult(
    int StatusCode,
    string? Code,
    AuditRecordPageV1? Page);

public sealed record AuditRecordDetailClientResult(
    int StatusCode,
    string? Code,
    AuditRecordDetailV1? Detail);
