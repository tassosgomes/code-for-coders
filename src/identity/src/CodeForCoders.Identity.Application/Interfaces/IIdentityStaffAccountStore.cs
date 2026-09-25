using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdentityStaffAccountStore
{
    Task<int> CountInternalStaffMembersAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMemberRecord>> ListInternalStaffMembersAsync(
        Guid tenantId,
        int page,
        int size,
        CancellationToken cancellationToken);

    Task<Account?> FindActiveAccountByNormalizedEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<bool> HasAdministratorAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Account?> FindInternalAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<StaffRoleAssignment?> FindRoleAssignmentAsync(
        Guid tenantId,
        Guid accountId,
        string role,
        CancellationToken cancellationToken);

    Task<List<StaffSession>> FindUnrevokedStaffSessionsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    void AddAccount(Account account);

    void AddCredential(Credential credential);

    void AddRoleAssignment(StaffRoleAssignment assignment);

    void RemoveRoleAssignment(StaffRoleAssignment assignment);
}

public sealed record StaffMemberRecord(
    Guid AccountId,
    string Name,
    string Email,
    IReadOnlyList<string> Roles);
