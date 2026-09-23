using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdentityRegistrationStore
{
    Task<IdempotencyRecord?> FindIdempotencyAsync(
        Guid tenantId,
        string operationId,
        string keyHash,
        CancellationToken cancellationToken);

    Task<bool> HasActiveAccountAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    void AddRegistration(Account account, Credential credential, VerificationToken token);

    void AddIdempotencyRecord(IdempotencyRecord record);
}
