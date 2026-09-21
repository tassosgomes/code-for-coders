namespace CodeForCoders.BffAdmin.Application.Common;

public sealed record OpaqueBffSession(
    string SessionId,
    string SubjectId,
    string UpstreamAccessToken,
    DateTimeOffset ExpiresAt);
