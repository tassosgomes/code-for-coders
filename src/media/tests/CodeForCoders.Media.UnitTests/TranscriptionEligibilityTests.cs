using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class TranscriptionEligibilityTests
{
    [Fact]
    public void ReadyVideoWithoutAttemptsCanStart()
        => Assert.Equal(new TranscriptionState("not-requested", false, null, null, true), TranscriptionState.Derive("ready", [], true));

    [Theory]
    [InlineData("received")]
    [InlineData("preparing")]
    [InlineData("failed")]
    public void NonReadyVideoCannotStart(string status)
        => Assert.Equal(new TranscriptionState("not-requested", false, null, null, false), TranscriptionState.Derive(status, [Attempt()], true));

    [Fact]
    public void DisabledFeatureHidesExistingAttempts()
        => Assert.Equal(new TranscriptionState("not-requested", false, null, null, false), TranscriptionState.Derive("ready", [Attempt()], false));

    [Fact]
    public void QueuedAttemptIsInProgress()
        => Assert.Equal(new TranscriptionState("in-progress", false, null, null, false), TranscriptionState.Derive("ready", [Attempt()], true));

    [Theory]
    [InlineData("duration-limit", false)]
    [InlineData("size-limit", false)]
    [InlineData("service-unavailable", true)]
    [InlineData("processing-failed", true)]
    [InlineData("timed-out", true)]
    public void FailureReasonControlsEligibility(string reason, bool canStart)
    {
        var attempt = Attempt(); attempt.MarkFailed(reason);
        Assert.Equal(new TranscriptionState("failed", false, null, reason, canStart), TranscriptionState.Derive("ready", [attempt], true));
    }

    [Fact]
    public void LatestAttemptWinsAndAnOngoingAttemptTakesPriority()
    {
        var first = Attempt(); first.MarkFailed("duration-limit");
        var second = Attempt();
        Assert.Equal("in-progress", TranscriptionState.Derive("ready", [first, second], true).Status);
        second.MarkFailed("service-unavailable");
        Assert.True(TranscriptionState.Derive("ready", [first, second], true).CanStart);
    }

    [Fact]
    public void RequestedAttemptRequiresAnActor()
        => Assert.Throws<EntityValidationException>(() => Transcription.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "requested", null, DateTimeOffset.UtcNow)));

    [Fact]
    public void TerminalAttemptCannotFailAgain()
    {
        var attempt = Attempt(); attempt.MarkFailed("processing-failed");
        Assert.Throws<EntityValidationException>(() => attempt.MarkFailed("timed-out"));
    }

    private static Transcription Attempt() => Transcription.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "requested", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
}
