using CodeForCoders.Commerce.Domain.Entities;
using Xunit;
namespace CodeForCoders.Commerce.UnitTests;

public sealed class AccessTermTests
{
    [Theory(DisplayName = nameof(CalendarTermUsesLocalDateAndExclusiveNextDay))]
    [InlineData("2027-03-15T15:00:00Z", 12, "2028-03-15", "2028-03-16T03:00:00Z")]
    [InlineData("2027-03-15T15:00:00Z", 1, "2027-04-15", "2027-04-16T03:00:00Z")]
    [InlineData("2027-01-31T15:00:00Z", 1, "2027-02-28", "2027-03-01T03:00:00Z")]
    [InlineData("2028-01-31T15:00:00Z", 1, "2028-02-29", "2028-03-01T03:00:00Z")]
    [InlineData("2027-03-31T15:00:00Z", 6, "2027-09-30", "2027-10-01T03:00:00Z")]
    [InlineData("2027-03-16T02:59:00Z", 1, "2027-04-15", "2027-04-16T03:00:00Z")]
    public void CalendarTermUsesLocalDateAndExclusiveNextDay(string now, int months, string endsOn, string expiresAt)
    {
        var term = AccessTerm.Calculate(DateTimeOffset.Parse(now), "months", months, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        Assert.Equal(DateOnly.Parse(endsOn), term.EndsOn); Assert.Equal(DateTimeOffset.Parse(expiresAt), term.ExpiresAt);
    }
    [Fact(DisplayName = nameof(LifetimeHasNoEnd))]
    public void LifetimeHasNoEnd() => Assert.Equal(new AccessTerm(null, null), AccessTerm.Calculate(DateTimeOffset.UtcNow, "lifetime", null, TimeZoneInfo.Utc));

    [Fact(DisplayName = nameof(NonexistentMidnightStartsAtFirstValidInstant))]
    public void NonexistentMidnightStartsAtFirstValidInstant()
    {
        // Brazil DST started at midnight on 2018-11-04.
        var term = AccessTerm.Calculate(DateTimeOffset.Parse("2018-10-03T15:00:00Z"), "months", 1, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        Assert.Equal(DateTimeOffset.Parse("2018-11-04T03:00:00Z"), term.ExpiresAt);
    }
    [Fact(DisplayName = nameof(AmbiguousMidnightUsesEarliestOccurrence))]
    public void AmbiguousMidnightUsesEarliestOccurrence()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 1);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 11, 1);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromHours(1), start, end);
        var zone = TimeZoneInfo.CreateCustomTimeZone("TestOverlap", TimeSpan.FromHours(-5), "TestOverlap", "Standard", "Daylight", [rule]);
        var term = AccessTerm.Calculate(DateTimeOffset.Parse("2026-09-30T16:00:00Z"), "months", 1, zone);
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T04:00:00Z"), term.ExpiresAt);
        var ambiguous = new DateTime(2026, 11, 1, 0, 0, 0);
        Assert.True(zone.IsAmbiguousTime(ambiguous));
        // October 1 + one month ends November 1, so use a zero-based shifted adjustment end on November 2.
        var nextEnd = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 11, 2);
        var nextRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromHours(1), start, nextEnd);
        var nextZone = TimeZoneInfo.CreateCustomTimeZone("NextOverlap", TimeSpan.FromHours(-5), "NextOverlap", "Standard", "Daylight", [nextRule]);
        var overlap = AccessTerm.Calculate(DateTimeOffset.Parse("2026-10-01T16:00:00Z"), "months", 1, nextZone);
        Assert.Equal(DateTimeOffset.Parse("2026-11-02T04:00:00Z"), overlap.ExpiresAt);
    }
}
