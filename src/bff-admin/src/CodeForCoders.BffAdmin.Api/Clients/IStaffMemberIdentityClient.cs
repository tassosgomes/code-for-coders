using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IStaffMemberIdentityClient
{
    Task<StaffMemberIdentityResult> ListStaffMembersAsync(
        Guid identitySessionId,
        int page,
        int size,
        CancellationToken cancellationToken);

    Task<StaffMemberIdentityResult> GrantRoleAsync(
        Guid accountId,
        StaffRoleActionRequestV1 request,
        Guid identitySessionId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StaffMemberIdentityResult> RevokeRoleAsync(
        Guid accountId,
        StaffRoleActionRequestV1 request,
        Guid identitySessionId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
