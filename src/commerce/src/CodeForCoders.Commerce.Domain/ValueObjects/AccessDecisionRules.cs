namespace CodeForCoders.Commerce.Domain.ValueObjects;

public static class AccessDecisionRules
{
    public static AccessDecision Decide(AccessGrantValiditySummary? grants, DateTimeOffset now)
    {
        if (grants is null)
            return new("denied", null, null, "no-grant", null, now);
        if (grants.HasLifetime)
            return new("allowed", "lifetime", null, null, null, now);
        if (grants.LatestExpiresAt > now)
            return new("allowed", "until", grants.LatestExpiresAt, null, null, now);

        return new("denied", null, null, "grant-ended", grants.LatestExpiresAt, now);
    }
}
