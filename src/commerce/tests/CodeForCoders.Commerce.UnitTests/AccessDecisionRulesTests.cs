using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.Entitlement.DecideAccess;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Moq;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

[Trait("Domain", "AccessDecisionRules - Unit")]
public sealed class AccessDecisionRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 3, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = nameof(NoGrantsDeniesWithoutAnExpiry))]
    public void NoGrantsDeniesWithoutAnExpiry()
        => Assert.Equal(new("denied", null, null, "no-grant", null, Now), AccessDecisionRules.Decide(null, Now));

    [Fact(DisplayName = nameof(LifetimeOverridesAnyFiniteGrant))]
    public void LifetimeOverridesAnyFiniteGrant()
        => Assert.Equal(new("allowed", "lifetime", null, null, null, Now),
            AccessDecisionRules.Decide(new(true, Now.AddMonths(3)), Now));

    [Fact(DisplayName = nameof(FutureExpiryAllowsUntilThatInstant))]
    public void FutureExpiryAllowsUntilThatInstant()
        => Assert.Equal(new("allowed", "until", Now.AddMonths(6), null, null, Now),
            AccessDecisionRules.Decide(new(false, Now.AddMonths(6)), Now));

    [Fact(DisplayName = nameof(LastSecondBeforeExpiryStillAllows))]
    public void LastSecondBeforeExpiryStillAllows()
        => Assert.Equal("allowed", AccessDecisionRules.Decide(new(false, Now), Now.AddSeconds(-1)).Decision);

    [Fact(DisplayName = nameof(ExpiryIsExclusiveAndReportsTheLastExpiry))]
    public void ExpiryIsExclusiveAndReportsTheLastExpiry()
        => Assert.Equal(new("denied", null, null, "grant-ended", Now, Now),
            AccessDecisionRules.Decide(new(false, Now), Now));

    [Fact(DisplayName = nameof(ExpiredGrantDeniesWithoutAnExpirationEvent))]
    public void ExpiredGrantDeniesWithoutAnExpirationEvent()
        => Assert.Equal(new("denied", null, null, "grant-ended", Now.AddDays(-1), Now),
            AccessDecisionRules.Decide(new(false, Now.AddDays(-1)), Now));

    [Fact(DisplayName = nameof(UseCaseReadsTheInjectedClockAfterReadingTheGrants))]
    public async Task UseCaseReadsTheInjectedClockAfterReadingTheGrants()
    {
        var clock = new DecisionClock(Now.AddSeconds(-1));
        var input = new DecideAccessInput(Guid.CreateVersion7(), Guid.CreateVersion7());
        var queries = new Mock<IAccessDecisionQueries>(MockBehavior.Strict);
        queries.Setup(query => query.FindAsync(new(input.StudentId, input.CourseId), TestContext.Current.CancellationToken))
            .Callback(() => clock.Now = Now).ReturnsAsync(new AccessGrantValiditySummary(false, Now));

        var result = await new DecideAccess(queries.Object, clock).ExecuteAsync(input, TestContext.Current.CancellationToken);

        Assert.Equal("denied", result.Decision);
        Assert.Equal("grant-ended", result.DeniedReason);
        Assert.Equal(Now, result.DecidedAt);
    }

    private sealed class DecisionClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
