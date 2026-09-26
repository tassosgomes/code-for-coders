using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdentityPasswordRecoveryStore
{
    Task<IdempotencyRecord?> FindIdempotencyAsync(
        Guid tenantId,
        string operationId,
        string keyHash,
        CancellationToken cancellationToken);

    Task<Account?> FindEligibleStudentByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<Account?> FindEligibleStaffByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<VerificationToken?> FindVerificationTokenAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken);

    Task<Account?> FindStudentAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<Credential?> FindCredentialAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<StudentSession?> FindActiveStudentSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<List<VerificationToken>> FindUnconsumedVerificationTokensAsync(
        Guid tenantId,
        Guid accountId,
        string purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<List<StudentSession>> FindUnrevokedStudentSessionsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    void AddVerificationToken(VerificationToken token);

    void AddCredential(Credential credential);

    void AddIdempotencyRecord(IdempotencyRecord record);
}
