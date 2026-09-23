namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;

public sealed record ResetStudentPasswordInput(Guid TenantId, string Token, string NewPassword, string IdempotencyKey);
