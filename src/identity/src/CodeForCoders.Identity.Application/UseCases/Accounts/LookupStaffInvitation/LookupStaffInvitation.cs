using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStaffInvitation;

public sealed class LookupStaffInvitation(
    IIdentityStaffInvitationStore invitationStore,
    TimeProvider timeProvider,
    IValidator<LookupStaffInvitationInput> validator) : ILookupStaffInvitation
{
    public async Task<LookupStaffInvitationOutput> ExecuteAsync(
        LookupStaffInvitationInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var invitation = await invitationStore.FindByTokenHashAsync(
            input.TenantId,
            HashToken(input.Token),
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (invitation is null
            || invitation.ExpiresOn <= now
            || invitation.AcceptedOn is not null
            || invitation.SupersededOn is not null)
        {
            throw InvalidInvitation();
        }

        return new LookupStaffInvitationOutput(invitation.OfferedRole, invitation.ExpiresOn);
    }

    internal static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    internal static StaffInvitationException InvalidInvitation()
        => new(422, "INVITATION_INVALID", "This invitation is no longer valid.");
}
