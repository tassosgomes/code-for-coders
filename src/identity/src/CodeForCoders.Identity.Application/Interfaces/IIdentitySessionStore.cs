using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdentitySessionStore
{
    Task<IdempotencyRecord?> FindIdempotencyAsync(
        Guid tenantId,
        string operationId,
        string keyHash,
        CancellationToken cancellationToken);

    Task<(Account? Account, Credential? Credential)> FindStudentCredentialAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<(Account? Account, Credential? Credential)> FindInternalCredentialAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetStaffRolesAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<StudentSessionDetails?> RenewActiveSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken);

    Task<StaffSessionDetails?> RenewActiveStaffSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset revokedOn,
        CancellationToken cancellationToken);

    Task RevokeStaffSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset revokedOn,
        CancellationToken cancellationToken);

    void AddSession(StudentSession session);

    void AddStaffSession(StaffSession session);

    void AddIdempotencyRecord(IdempotencyRecord record);
}
