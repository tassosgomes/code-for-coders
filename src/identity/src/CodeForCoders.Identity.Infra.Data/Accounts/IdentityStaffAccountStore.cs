using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdentityStaffAccountStore(IdentityDbContext dbContext) : IIdentityStaffAccountStore
{
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

    public void AddRoleAssignment(StaffRoleAssignment assignment)
        => dbContext.StaffRoleAssignments.Add(assignment);
}
