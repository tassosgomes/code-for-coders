using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdentityRegistrationStore(IdentityDbContext dbContext) : IIdentityRegistrationStore
{
    public Task<IdempotencyRecord?> FindIdempotencyAsync(
        Guid tenantId,
        string operationId,
        string keyHash,
        CancellationToken cancellationToken)
        => dbContext.IdempotencyRecords.SingleOrDefaultAsync(
            record => record.TenantId == tenantId
                && record.OperationId == operationId
                && record.KeyHash == keyHash,
            cancellationToken);

    public Task<bool> HasActiveAccountAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => dbContext.Accounts.AnyAsync(
            account => account.TenantId == tenantId
                && account.NormalizedEmail == normalizedEmail
                && account.DeactivatedOn == null,
            cancellationToken);

    public void AddRegistration(Account account, Credential credential, VerificationToken token)
    {
        dbContext.Accounts.Add(account);
        dbContext.Credentials.Add(credential);
        dbContext.VerificationTokens.Add(token);
    }

    public void AddIdempotencyRecord(IdempotencyRecord record)
        => dbContext.IdempotencyRecords.Add(record);
}
