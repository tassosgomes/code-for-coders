namespace CodeForCoders.Identity.Application.Common;

public sealed record StaffSessionDetails(
    Guid SessionId,
    Guid AccountId,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset ExpiresAt);
