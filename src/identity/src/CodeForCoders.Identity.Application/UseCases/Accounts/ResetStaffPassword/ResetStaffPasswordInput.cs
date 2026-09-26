namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResetStaffPassword;

public sealed record ResetStaffPasswordInput(Guid TenantId, string Token, string NewPassword, string IdempotencyKey);
