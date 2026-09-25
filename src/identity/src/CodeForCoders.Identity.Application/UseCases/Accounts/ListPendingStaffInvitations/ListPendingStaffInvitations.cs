using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListPendingStaffInvitations;

public sealed class ListPendingStaffInvitations(
    IIdentityStaffInvitationStore invitationStore,
    TimeProvider timeProvider,
    IValidator<ListPendingStaffInvitationsInput> validator) : IListPendingStaffInvitations
{
    public async Task<ListPendingStaffInvitationsOutput> ExecuteAsync(
        ListPendingStaffInvitationsInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (input.Page < 1 || input.Size is < 1 or > 100)
        {
            throw new StaffInvitationException(400, "INVALID_REQUEST", "Page and size are outside the supported range.");
        }

        var now = timeProvider.GetUtcNow();
        var total = await invitationStore.CountPendingAsync(input.TenantId, now, cancellationToken);
        var invitations = await invitationStore.ListPendingAsync(
            input.TenantId,
            now,
            input.Page,
            input.Size,
            cancellationToken);
        return new ListPendingStaffInvitationsOutput(
            invitations.Select(invitation => new PendingStaffInvitationOutput(
                invitation.Id,
                invitation.Email,
                invitation.OfferedRole,
                invitation.InvitedOn,
                invitation.ExpiresOn)).ToArray(),
            new InvitationPaginationOutput(
                input.Page,
                input.Size,
                total,
                total == 0 ? 0 : (int)Math.Ceiling(total / (double)input.Size)));
    }
}
