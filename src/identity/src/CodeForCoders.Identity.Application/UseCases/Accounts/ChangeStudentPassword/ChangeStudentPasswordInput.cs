namespace CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStudentPassword;

public sealed record ChangeStudentPasswordInput(
    Guid TenantId,
    Guid SessionId,
    string CurrentPassword,
    string NewPassword,
    string IdempotencyKey);
