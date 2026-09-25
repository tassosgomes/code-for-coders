namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStaffInvitation;

public sealed record LookupStaffInvitationInput(Guid TenantId, string Token);
