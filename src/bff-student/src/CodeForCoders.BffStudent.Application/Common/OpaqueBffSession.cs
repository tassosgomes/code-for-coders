namespace CodeForCoders.BffStudent.Application.Common;

public sealed record OpaqueBffSession(
    Guid StudentSessionId,
    Guid AccountId,
    string Name,
    string CsrfToken,
    DateTimeOffset ExpiresAt);
