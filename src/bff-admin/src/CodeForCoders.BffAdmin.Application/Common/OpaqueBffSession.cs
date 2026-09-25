namespace CodeForCoders.BffAdmin.Application.Common;

public sealed record OpaqueBffSession(
    string SessionId,
    Guid IdentitySessionId,
    string CsrfToken,
    DateTimeOffset ExpiresAt);
