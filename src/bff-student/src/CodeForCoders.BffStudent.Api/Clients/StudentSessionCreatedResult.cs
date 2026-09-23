namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record StudentSessionCreatedResult(
    int StatusCode,
    string? Code,
    Guid SessionId = default,
    Guid AccountId = default,
    string? Name = null,
    DateTimeOffset ExpiresAt = default);
