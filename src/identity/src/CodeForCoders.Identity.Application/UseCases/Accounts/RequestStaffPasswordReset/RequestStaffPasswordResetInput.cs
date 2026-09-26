namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStaffPasswordReset;

public sealed record RequestStaffPasswordResetInput(Guid TenantId, string Email, string IdempotencyKey);
