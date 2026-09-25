namespace CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;

public sealed record CreateStaffInvitationOutput(
    Guid InvitationId,
    string Email,
    string OfferedRole,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    Guid? SupersededInvitationId);
