using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdentityStaffAccountStore
{
    Task<Account?> FindActiveAccountByNormalizedEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<bool> HasAdministratorAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Account?> FindInternalAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<List<StaffSession>> FindUnrevokedStaffSessionsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    void AddAccount(Account account);

    void AddCredential(Credential credential);

    void AddRoleAssignment(StaffRoleAssignment assignment);
}
