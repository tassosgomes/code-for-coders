namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;

public sealed record AuthenticateStudentSessionInput(
    Guid TenantId,
    string Email,
    string Password,
    string IdempotencyKey);
