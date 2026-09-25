namespace CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;

public sealed record CreateStaffInvitationInput(
    Guid TenantId,
    Guid ActorAccountId,
    string Email,
    string Role,
    string Reason,
    string IdempotencyKey);
