namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;

public sealed record RequestStudentPasswordResetInput(Guid TenantId, string Email, string IdempotencyKey);
