namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;

public sealed record AuthenticateStaffSessionInput(
    Guid TenantId,
    string Email,
    string Password,
    string IdempotencyKey);
