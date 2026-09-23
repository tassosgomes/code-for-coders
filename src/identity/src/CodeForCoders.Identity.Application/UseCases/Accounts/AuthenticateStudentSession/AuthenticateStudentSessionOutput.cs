namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;

public sealed record AuthenticateStudentSessionOutput(
    Guid SessionId,
    Guid AccountId,
    string Name,
    DateTimeOffset ExpiresAt);
