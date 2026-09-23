namespace CodeForCoders.Identity.Application.Common;

public sealed record StudentSessionDetails(
    Guid SessionId,
    Guid AccountId,
    string Name,
    DateTimeOffset ExpiresAt);
