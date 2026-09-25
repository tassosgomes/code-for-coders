using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdentityStaffAccountStore(IdentityDbContext dbContext) : IIdentityStaffAccountStore
{
    public async Task<int> CountInternalStaffMembersAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await dbContext.Accounts.CountAsync(
            account => account.TenantId == tenantId
                && account.Type == AccountType.InternalActor
                && account.DeactivatedOn == null,
            cancellationToken);

    public async Task<IReadOnlyList<StaffMemberRecord>> ListInternalStaffMembersAsync(
        Guid tenantId,
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        var offset = (long)(page - 1) * size;
        if (offset > int.MaxValue)
        {
            return [];
        }

        var accounts = await dbContext.Accounts.AsNoTracking()
            .Where(account => account.TenantId == tenantId
                && account.Type == AccountType.InternalActor
                && account.DeactivatedOn == null)
            .OrderBy(account => account.Name)
            .ThenBy(account => account.Id)
            .Skip((int)offset)
            .Take(size)
            .Select(account => new { account.Id, account.Name, account.Email })
            .ToListAsync(cancellationToken);
        if (accounts.Count == 0)
        {
            return [];
        }

        var accountIds = accounts.Select(account => account.Id).ToArray();
        var assignments = await dbContext.StaffRoleAssignments.AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && accountIds.Contains(assignment.AccountId))
            .OrderBy(assignment => assignment.Role)
            .Select(assignment => new { assignment.AccountId, assignment.Role })
            .ToListAsync(cancellationToken);
        var rolesByAccount = assignments
            .GroupBy(assignment => assignment.AccountId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(assignment => assignment.Role).ToArray());

        return accounts
            .Select(account => new StaffMemberRecord(
                account.Id,
                account.Name,
                account.Email,
                rolesByAccount.GetValueOrDefault(account.Id, Array.Empty<string>())))
            .ToArray();
    }

    public Task<Account?> FindActiveAccountByNormalizedEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => dbContext.Accounts.SingleOrDefaultAsync(
            account => account.TenantId == tenantId
                && account.NormalizedEmail == normalizedEmail
                && account.DeactivatedOn == null,
            cancellationToken);

    public Task<bool> HasAdministratorAsync(Guid tenantId, CancellationToken cancellationToken)
        => dbContext.StaffRoleAssignments.AnyAsync(
            assignment => assignment.TenantId == tenantId
                && assignment.Role == StaffRoleCatalog.Administrator,
            cancellationToken);

    public Task<Account?> FindInternalAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => dbContext.Accounts.SingleOrDefaultAsync(
            account => account.TenantId == tenantId
                && account.Id == accountId
                && account.Type == AccountType.InternalActor
                && account.DeactivatedOn == null,
            cancellationToken);

    public Task<StaffRoleAssignment?> FindRoleAssignmentAsync(
        Guid tenantId,
        Guid accountId,
        string role,
        CancellationToken cancellationToken)
        => dbContext.StaffRoleAssignments.SingleOrDefaultAsync(
            assignment => assignment.TenantId == tenantId
                && assignment.AccountId == accountId
                && assignment.Role == role,
            cancellationToken);

    public Task<List<StaffSession>> FindUnrevokedStaffSessionsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => dbContext.StaffSessions
            .Where(session => session.TenantId == tenantId
                && session.AccountId == accountId
                && session.RevokedOn == null)
            .ToListAsync(cancellationToken);

    public void AddAccount(Account account) => dbContext.Accounts.Add(account);

    public void AddCredential(Credential credential) => dbContext.Credentials.Add(credential);

    public void AddRoleAssignment(StaffRoleAssignment assignment)
        => dbContext.StaffRoleAssignments.Add(assignment);

    public void RemoveRoleAssignment(StaffRoleAssignment assignment)
        => dbContext.StaffRoleAssignments.Remove(assignment);
}
