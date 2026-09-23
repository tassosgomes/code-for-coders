using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdentitySessionStore(IdentityDbContext dbContext) : IIdentitySessionStore
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

    public async Task<(Account? Account, Credential? Credential)> FindStudentCredentialAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            candidate => candidate.TenantId == tenantId
                && candidate.NormalizedEmail == normalizedEmail
                && candidate.DeactivatedOn == null,
            cancellationToken);
        if (account is null)
        {
            return (null, null);
        }

        var credential = account.Type == AccountType.Student
            ? await dbContext.Credentials.SingleOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.AccountId == account.Id,
                cancellationToken)
            : null;
        return (account, credential);
    }

    public async Task<StudentSessionDetails?> RenewActiveSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken)
    {
        var updated = await dbContext.StudentSessions
            .Where(session => session.TenantId == tenantId
                && session.Id == sessionId
                && session.RevokedOn == null
                && session.ExpiresOn > now
                && dbContext.Accounts.Any(account => account.TenantId == tenantId
                    && account.Id == session.AccountId
                    && account.Type == AccountType.Student
                    && account.IsConfirmed
                    && account.DeactivatedOn == null))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.LastActivityOn, now)
                .SetProperty(session => session.ExpiresOn, expiresOn), cancellationToken);
        if (updated != 1)
        {
            return null;
        }

        return await (
                from session in dbContext.StudentSessions.AsNoTracking()
                join account in dbContext.Accounts.AsNoTracking()
                    on new { session.TenantId, session.AccountId } equals new { account.TenantId, AccountId = account.Id }
                where session.TenantId == tenantId
                    && session.Id == sessionId
                    && session.RevokedOn == null
                    && session.ExpiresOn > now
                    && account.Type == AccountType.Student
                    && account.IsConfirmed
                    && account.DeactivatedOn == null
                select new StudentSessionDetails(session.Id, account.Id, account.Name, session.ExpiresOn))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset revokedOn,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.StudentSessions.SingleOrDefaultAsync(
            candidate => candidate.TenantId == tenantId && candidate.Id == sessionId,
            cancellationToken);
        session?.Revoke(revokedOn);
    }

    public void AddSession(StudentSession session) => dbContext.StudentSessions.Add(session);

    public void AddIdempotencyRecord(IdempotencyRecord record) => dbContext.IdempotencyRecords.Add(record);
}
