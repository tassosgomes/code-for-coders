namespace CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;

public sealed record RegisterStudentAccountInput(
    Guid TenantId,
    string Name,
    string Email,
    string Password,
    string IdempotencyKey);
