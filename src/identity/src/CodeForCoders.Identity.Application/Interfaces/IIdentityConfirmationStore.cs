using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdentityConfirmationStore
{
    Task<IdempotencyRecord?> FindIdempotencyAsync(
        Guid tenantId,
        string operationId,
        string keyHash,
        CancellationToken cancellationToken);

    Task<VerificationToken?> FindVerificationTokenAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken);

    Task<Account?> FindStudentAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<Account?> FindEligibleStudentByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    void RequireStillUnconfirmed(Account account);

    void AddVerificationToken(VerificationToken token);

    void AddIdempotencyRecord(IdempotencyRecord record);
}
