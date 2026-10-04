using CodeForCoders.Media.Domain.Entities;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

[Trait("Layer", "Playback session renewal - Unit")]
public sealed class PlaybackSessionRenewalTests
{
    [Fact(DisplayName = nameof(RenewalStartsFiveMinutesAtTheCurrentInstant))]
    public void RenewalStartsFiveMinutesAtTheCurrentInstant()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Create(now); var renewal = now.AddSeconds(210);
        Assert.True(session.TryRenew(renewal)); Assert.Equal(renewal.AddMinutes(5), session.ExpiresAt);
        Assert.True(session.TryRenew(renewal)); Assert.Equal(renewal.AddMinutes(5), session.ExpiresAt);
    }

    [Theory(DisplayName = nameof(ExpiredSessionsCannotBeRevived))]
    [InlineData(300)]
    [InlineData(301)]
    public void ExpiredSessionsCannotBeRevived(int seconds)
    {
        var now = DateTimeOffset.UtcNow; var session = Create(now);
        Assert.False(session.TryRenew(now.AddSeconds(seconds))); Assert.Equal(now.AddMinutes(5), session.ExpiresAt);
    }

    private static PlaybackSession Create(DateTimeOffset now)
        => PlaybackSession.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), now));
}
