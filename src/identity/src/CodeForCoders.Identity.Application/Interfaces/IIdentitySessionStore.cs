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

    /// <summary>
    /// Enlists the authenticated credential in the current unit of work under its optimistic
    /// concurrency token, without changing its data, so a concurrent password reset/change that
    /// bumped the credential makes this commit fail with a concurrency conflict (RACE #8).
    /// </summary>
    void TouchAuthenticatedCredential(Credential credential);

    void AddIdempotencyRecord(IdempotencyRecord record);
}
