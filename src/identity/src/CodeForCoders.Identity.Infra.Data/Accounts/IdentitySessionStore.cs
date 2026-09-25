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

    public async Task<(Account? Account, Credential? Credential)> FindInternalCredentialAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            candidate => candidate.TenantId == tenantId
                && candidate.NormalizedEmail == normalizedEmail
                && candidate.Type == AccountType.InternalActor
                && candidate.DeactivatedOn == null,
            cancellationToken);
        if (account is null)
        {
            return (null, null);
        }

        var credential = await dbContext.Credentials.SingleOrDefaultAsync(
            candidate => candidate.TenantId == tenantId && candidate.AccountId == account.Id,
            cancellationToken);
        return (account, credential);
    }

    public async Task<IReadOnlyList<string>> GetStaffRolesAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => await dbContext.StaffRoleAssignments
            .Where(assignment => assignment.TenantId == tenantId && assignment.AccountId == accountId)
            .OrderBy(assignment => assignment.Role)
            .Select(assignment => assignment.Role)
            .ToListAsync(cancellationToken);

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

    public async Task<StaffSessionDetails?> RenewActiveStaffSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken)
    {
        var updated = await dbContext.StaffSessions
            .Where(session => session.TenantId == tenantId
                && session.Id == sessionId
                && session.RevokedOn == null
                && session.ExpiresOn > now
                && dbContext.Accounts.Any(account => account.TenantId == tenantId
                    && account.Id == session.AccountId
                    && account.Type == AccountType.InternalActor
                    && account.DeactivatedOn == null))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.ExpiresOn, expiresOn), cancellationToken);
        if (updated != 1)
        {
            return null;
        }

        var session = await dbContext.StaffSessions.AsNoTracking()
            .Where(candidate => candidate.TenantId == tenantId
                && candidate.Id == sessionId
                && candidate.RevokedOn == null
                && candidate.ExpiresOn > now)
            .Join(
                dbContext.Accounts.AsNoTracking(),
                candidate => new { candidate.TenantId, candidate.AccountId },
                account => new { account.TenantId, AccountId = account.Id },
                (candidate, account) => new { Session = candidate, Account = account })
            .Where(pair => pair.Account.Type == AccountType.InternalActor
                && pair.Account.DeactivatedOn == null)
            .Select(pair => new { pair.Session.Id, pair.Session.TenantId, pair.Session.AccountId, pair.Account.Name, pair.Session.ExpiresOn })
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        var roles = await GetStaffRolesAsync(tenantId, session.AccountId, cancellationToken);
        var permissions = roles
            .SelectMany(StaffRoleCatalog.GetPermissions)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new StaffSessionDetails(
            session.Id,
            session.AccountId,
            session.Name,
            roles,
            permissions,
            session.ExpiresOn);
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

    public async Task RevokeStaffSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset revokedOn,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.StaffSessions.SingleOrDefaultAsync(
            candidate => candidate.TenantId == tenantId && candidate.Id == sessionId,
            cancellationToken);
        session?.Revoke(revokedOn);
    }

    public void AddSession(StudentSession session) => dbContext.StudentSessions.Add(session);

    public void AddStaffSession(StaffSession session) => dbContext.StaffSessions.Add(session);

    public void AddIdempotencyRecord(IdempotencyRecord record) => dbContext.IdempotencyRecords.Add(record);
}
