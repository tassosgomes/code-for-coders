using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdentityConfirmationStore(IdentityDbContext dbContext) : IIdentityConfirmationStore
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

    public Task<VerificationToken?> FindVerificationTokenAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken)
        => dbContext.VerificationTokens.SingleOrDefaultAsync(
            token => token.TenantId == tenantId && token.TokenHash == tokenHash,
            cancellationToken);

    public Task<Account?> FindStudentAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => dbContext.Accounts.SingleOrDefaultAsync(
            account => account.TenantId == tenantId
                && account.Id == accountId
                && account.Type == AccountType.Student,
            cancellationToken);

    public Task<Account?> FindEligibleStudentByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => dbContext.Accounts.SingleOrDefaultAsync(
            account => account.TenantId == tenantId
                && account.NormalizedEmail == normalizedEmail
                && account.Type == AccountType.Student
                && !account.IsConfirmed
                && account.DeactivatedOn == null,
            cancellationToken);

    public void RequireStillUnconfirmed(Account account)
        => dbContext.Entry(account).Property(candidate => candidate.IsConfirmed).IsModified = true;

    public void AddVerificationToken(VerificationToken token) => dbContext.VerificationTokens.Add(token);

    public void AddIdempotencyRecord(IdempotencyRecord record) => dbContext.IdempotencyRecords.Add(record);
}
