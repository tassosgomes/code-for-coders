namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;

public sealed record ValidateStudentSessionInput(Guid TenantId, Guid SessionId);
