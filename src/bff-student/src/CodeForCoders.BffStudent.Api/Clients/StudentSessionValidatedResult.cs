namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record StudentSessionValidatedResult(
    int StatusCode,
    string? Code,
    Guid AccountId = default,
    string? Name = null,
    DateTimeOffset ExpiresAt = default,
    string? AccessToken = null);
