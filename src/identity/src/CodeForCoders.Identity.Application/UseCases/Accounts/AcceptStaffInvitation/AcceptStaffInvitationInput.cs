namespace CodeForCoders.Identity.Application.UseCases.Accounts.AcceptStaffInvitation;

public sealed record AcceptStaffInvitationInput(
    Guid TenantId,
    string Token,
    string Name,
    string Password,
    string IdempotencyKey);
