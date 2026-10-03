using CodeForCoders.Commerce.Domain.Entities;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

[Trait("Domain", "Access grant - Expiry fact invariant")]
public sealed class AccessGrantExpiryFactTests
{
    [Fact(DisplayName = nameof(ExpiryFactCannotBeMarkedBeforeExclusiveEnd))]
    public void ExpiryFactCannotBeMarkedBeforeExclusiveEnd()
    {
        var grant = CreateGrant("months");
        Assert.Throws<EntitlementRuleException>(() => grant.MarkExpiryFact(Guid.CreateVersion7(), grant.ExpiresAt!.Value.AddTicks(-1)));
        Assert.Null(grant.ExpiryEventId);
        Assert.Null(grant.ExpiryPublishedAt);
    }

    [Fact(DisplayName = nameof(LifetimeCannotHaveAnExpiryFact))]
    public void LifetimeCannotHaveAnExpiryFact()
    {
        var grant = CreateGrant("lifetime");
        Assert.Throws<EntitlementRuleException>(() => grant.MarkExpiryFact(Guid.CreateVersion7(), grant.GrantedAt.AddYears(100)));
        Assert.Null(grant.ExpiryEventId);
        Assert.Null(grant.ExpiryPublishedAt);
    }

    [Fact(DisplayName = nameof(ExpiryFactIdentityCannotBeEmptyOrReplaced))]
    public void ExpiryFactIdentityCannotBeEmptyOrReplaced()
    {
        var grant = CreateGrant("months");
        var now = grant.ExpiresAt!.Value;
        Assert.Throws<EntitlementRuleException>(() => grant.MarkExpiryFact(Guid.Empty, now));
        var eventId = Guid.CreateVersion7();
        grant.MarkExpiryFact(eventId, now);
        Assert.Throws<EntitlementRuleException>(() => grant.MarkExpiryFact(Guid.CreateVersion7(), now.AddMinutes(1)));
        Assert.Equal(eventId, grant.ExpiryEventId);
        Assert.Equal(now, grant.ExpiryPublishedAt);
        Assert.Equal("active", grant.Status);
    }

    private static AccessGrant CreateGrant(string period)
    {
        var now = DateTimeOffset.Parse("2026-10-15T14:00:00Z");
        var enrollment = Enrollment.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now);
        return AccessGrant.CreateCourtesy(enrollment,
            new(Guid.CreateVersion7(), "Expiration proof", period, period == "months" ? 1 : null, now), TimeZoneInfo.Utc);
    }
}
