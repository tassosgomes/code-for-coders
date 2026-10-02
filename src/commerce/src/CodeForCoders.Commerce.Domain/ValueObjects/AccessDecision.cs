namespace CodeForCoders.Commerce.Domain.ValueObjects;

public sealed record AccessDecision(string Decision, string? ValidityType, DateTimeOffset? ExpiresAt,
    string? DeniedReason, DateTimeOffset? LastExpiredAt, DateTimeOffset DecidedAt);
