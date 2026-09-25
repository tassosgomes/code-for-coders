using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IStaffInvitationMessageWriter
{
    Task AppendInvitationIssuedAsync(
        StaffInvitation invitation,
        string rawToken,
        Guid actorAccountId,
        string reason,
        DateTimeOffset issuedOn,
        CancellationToken cancellationToken);

    Task AppendInvitationAcceptedAsync(
        StaffInvitation invitation,
        Guid accountId,
        DateTimeOffset acceptedOn,
        CancellationToken cancellationToken);
}
