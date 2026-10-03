namespace CodeForCoders.Commerce.Domain.ValueObjects;

public sealed record AccessGrantValiditySummary(bool HasLifetime, DateTimeOffset? LatestExpiresAt);
