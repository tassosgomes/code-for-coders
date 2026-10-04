namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;

public sealed record ValidateStudentSessionOutput(
    Guid AccountId,
    string Name,
    DateTimeOffset ExpiresAt,
    string? Email = null);
