namespace CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccount;

public sealed record ConfirmStudentAccountInput(Guid TenantId, string Token, string IdempotencyKey);
