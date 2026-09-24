namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentAccountConfirmation;

public sealed record RequestStudentAccountConfirmationInput(Guid TenantId, string Email, string IdempotencyKey);
