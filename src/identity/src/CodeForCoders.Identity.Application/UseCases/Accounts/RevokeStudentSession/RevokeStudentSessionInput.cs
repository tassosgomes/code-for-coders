namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;

public sealed record RevokeStudentSessionInput(Guid TenantId, Guid SessionId, string IdempotencyKey);
