using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IStaffInvitationIdentityClient
{
    Task<StaffInvitationIdentityResult> CreateInvitationAsync(
        CreateStaffInvitationRequestV1 request,
        Guid identitySessionId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StaffInvitationIdentityResult> ListPendingInvitationsAsync(
        Guid identitySessionId,
        int page,
        int size,
        CancellationToken cancellationToken);

    Task<StaffInvitationIdentityResult> LookupInvitationAsync(
        InvitationTokenRequestV1 request,
        CancellationToken cancellationToken);

    Task<StaffInvitationIdentityResult> AcceptInvitationAsync(
        AcceptStaffInvitationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
