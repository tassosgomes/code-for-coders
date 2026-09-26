namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListPendingStaffInvitations;

public sealed record ListPendingStaffInvitationsInput(Guid TenantId, int Page, int Size);
