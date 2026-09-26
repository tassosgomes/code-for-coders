namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStaffInvitation;

public sealed record LookupStaffInvitationOutput(string OfferedRole, DateTimeOffset ExpiresAt);
