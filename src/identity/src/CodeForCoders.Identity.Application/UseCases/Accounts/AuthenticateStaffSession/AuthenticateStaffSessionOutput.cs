namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;

public sealed record AuthenticateStaffSessionOutput(
    Guid SessionId,
    Guid AccountId,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset ExpiresAt);
