using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdentityPasswordRecoveryStore(IdentityDbContext dbContext) : IIdentityPasswordRecoveryStore
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

    public async Task<Account?> FindEligibleStudentByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            candidate => candidate.TenantId == tenantId
                && candidate.NormalizedEmail == normalizedEmail
                && candidate.Type == AccountType.Student
                && candidate.DeactivatedOn == null,
            cancellationToken);
        if (account is null)
        {
            return null;
        }

        return await dbContext.Credentials.AnyAsync(
            credential => credential.TenantId == tenantId && credential.AccountId == account.Id,
            cancellationToken)
                ? account
                : null;
    }

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
                && account.Type == AccountType.Student
                && account.DeactivatedOn == null,
            cancellationToken);

    public Task<Credential?> FindCredentialAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => dbContext.Credentials.SingleOrDefaultAsync(
            credential => credential.TenantId == tenantId && credential.AccountId == accountId,
            cancellationToken);

    public Task<StudentSession?> FindActiveStudentSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => dbContext.StudentSessions.SingleOrDefaultAsync(
            session => session.TenantId == tenantId
                && session.Id == sessionId
                && session.RevokedOn == null
                && session.ExpiresOn > now,
            cancellationToken);

    public Task<List<VerificationToken>> FindUnconsumedVerificationTokensAsync(
        Guid tenantId,
        Guid accountId,
        string purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => dbContext.VerificationTokens
            .Where(token => token.TenantId == tenantId
                && token.AccountId == accountId
                && token.Purpose == purpose
                && token.ConsumedOn == null
                && token.ExpiresOn > now)
            .ToListAsync(cancellationToken);

    public Task<List<StudentSession>> FindUnrevokedStudentSessionsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => dbContext.StudentSessions
            .Where(session => session.TenantId == tenantId
                && session.AccountId == accountId
                && session.RevokedOn == null)
            .ToListAsync(cancellationToken);

    public void AddVerificationToken(VerificationToken token) => dbContext.VerificationTokens.Add(token);

    public void AddIdempotencyRecord(IdempotencyRecord record) => dbContext.IdempotencyRecords.Add(record);
}
