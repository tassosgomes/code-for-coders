namespace CodeForCoders.BffStudent.Contracts;

public sealed record StudentSessionLoginV1(string? Email, string? Password);

public sealed record StudentSessionReferenceV1(Guid SessionId);

public sealed record StudentSessionValidationV1(Guid SessionId, string? Audience);

public sealed record StudentSessionCreatedV1(
    Guid SessionId,
    Guid AccountId,
    string Name,
    DateTimeOffset ExpiresAt);

public sealed record StudentSessionValidatedV1(
    Guid AccountId,
    string Name,
    DateTimeOffset ExpiresAt,
    string? AccessToken);
