using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CodeForCoders.Media.IntegrationTests.TranscriptionTestHarness;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class TranscriptionFailurePolicyTests(VideoLibraryApiFactory factory)
{
    private static readonly TimeSpan[] SubmissionDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)];

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(422)]
    public async Task RefusedSubmissionStaysQueuedWithoutTouchingTheState(int status)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.SubmitStatusCode = status;
        await using var context = await CreateContextAsync(factory, server);
        var (_, id) = await EnqueuePreparedAsync(context); var before = DateTimeOffset.UtcNow;
        Assert.True(await ExecuteAsync(context));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("queued", attempt.Status); Assert.Equal(1, attempt.SubmissionFailures); Assert.Equal(0, attempt.Submissions);
        Assert.Null(attempt.WhisperJobId); Assert.Null(attempt.FailureReason); Assert.Null(attempt.LeaseId);
        AssertScheduledIn(attempt, SubmissionDelays[0], before);
    }

    [Fact]
    public async Task SubmissionFailuresBackOffOneFiveFifteenThirtyMinutesThenReportServiceUnavailable()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.SubmitStatusCode = 503;
        await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context);
        for (var failure = 1; failure <= 4; failure++)
        {
            var before = DateTimeOffset.UtcNow;
            Assert.True(await RetryAsync(context, id));
            var attempt = await AttemptAsync(context, id);
            Assert.Equal("queued", attempt.Status); Assert.Equal(failure, attempt.SubmissionFailures);
            AssertScheduledIn(attempt, SubmissionDelays[failure - 1], before);
        }
        Assert.True(await RetryAsync(context, id));
        var last = await AttemptAsync(context, id);
        Assert.Equal("failed", last.Status); Assert.Equal("service-unavailable", last.FailureReason);
        Assert.NotNull(last.AudioDeletedAt); await AssertCleanAsync(context, video);
        Assert.Equal(5, server.Submissions.Count);
        Assert.True(TranscriptionState.Derive("ready", [last], true).CanStart);
    }

    [Fact]
    public async Task SubmissionThatRecoversResetsTheExtractionCountAndWaitsForTheJob()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.SubmitStatusCode = 502;
        await using var context = await CreateContextAsync(factory, server);
        var (_, id) = await EnqueuePreparedAsync(context);
        Assert.True(await ExecuteAsync(context)); Assert.Equal("queued", (await AttemptAsync(context, id)).Status);
        server.SubmitStatusCode = 202; Assert.True(await RetryAsync(context, id));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("waiting", attempt.Status); Assert.Equal("trn_test", attempt.WhisperJobId); Assert.Equal(1, attempt.Submissions);
        Assert.Equal(1, attempt.SubmissionFailures); Assert.Equal(1, attempt.ExtractionAttempts);
    }

    [Fact]
    public async Task UnreachableServiceIsRetriedLikeAnyOtherRefusal()
    {
        var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await CreateContextAsync(factory, server);
        var (_, id) = await EnqueuePreparedAsync(context);
        await server.DisposeAsync();
        Assert.True(await ExecuteAsync(context));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("queued", attempt.Status); Assert.Equal(1, attempt.SubmissionFailures); Assert.Null(attempt.FailureReason);
    }

    [Fact]
    public async Task PayloadTooLargeOnSubmissionFailsWithSizeLimitAndNoRetry()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.SubmitStatusCode = 413;
        await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context);
        Assert.True(await ExecuteAsync(context));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("failed", attempt.Status); Assert.Equal("size-limit", attempt.FailureReason);
        Assert.False(TranscriptionState.Derive("ready", [attempt], true).CanStart);
        await AssertCleanAsync(context, video); Assert.False(await ExecuteAsync(context)); Assert.Single(server.Submissions);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task RejectedIntegrationHasItsOwnSignalAndNeverEchoesTheCredential(int status)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.SubmitStatusCode = status;
        await using var context = await CreateContextAsync(factory, server);
        await EnqueuePreparedAsync(context); var rejected = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => { if (instrument.Name == "media.transcription.integration_rejected") meterListener.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref rejected, (int)value)); listener.Start();
        Assert.True(await ExecuteAsync(context));
        Assert.Equal(1, rejected);
        var logs = string.Join(" ", context.Logs.Entries);
        Assert.Contains("rejected the integration", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(TranscriptionHttpSimulator.ApiKey, logs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MEDIA_DURATION_LIMIT_EXCEEDED", "duration-limit", false)]
    [InlineData("MEDIA_SIZE_LIMIT_EXCEEDED", "size-limit", false)]
    [InlineData("UNSUPPORTED_MEDIA_FORMAT", "processing-failed", true)]
    [InlineData("TRANSCRIPTION_FAILED", "processing-failed", true)]
    [InlineData("A_CODE_NOBODY_KNOWS", "processing-failed", true)]
    [InlineData(null, "processing-failed", true)]
    public async Task FailedJobEndsInTheMappedReasonWithoutAnotherSubmission(string? code, string reason, bool canStart)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context); Assert.True(await ExecuteAsync(context));
        server.Status = "failed"; server.FailureCode = code;
        Assert.True(await RetryAsync(context, id));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("failed", attempt.Status); Assert.Equal(reason, attempt.FailureReason);
        Assert.Equal(canStart, TranscriptionState.Derive("ready", [attempt], true).CanStart);
        Assert.Single(server.Submissions); Assert.NotNull(attempt.AudioDeletedAt); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task LostSourceReextractsAndResubmitsAfterFiveThenThirtyMinutesThenReportsServiceUnavailable()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context); server.Status = "failed"; server.FailureCode = "SOURCE_UNAVAILABLE";
        Assert.True(await ExecuteAsync(context));
        var delays = new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30) };
        for (var submission = 1; submission <= 2; submission++)
        {
            var before = DateTimeOffset.UtcNow; Assert.True(await RetryAsync(context, id));
            var requeued = await AttemptAsync(context, id);
            Assert.Equal("queued", requeued.Status); Assert.Null(requeued.WhisperJobId); Assert.Null(requeued.SubmittedAt); Assert.Null(requeued.DeadlineAt);
            Assert.Equal(submission, requeued.Submissions); Assert.Equal(0, requeued.ExtractionAttempts);
            AssertScheduledIn(requeued, delays[submission - 1], before);
            Assert.Empty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
            Assert.True(await RetryAsync(context, id));
            Assert.Equal("waiting", (await AttemptAsync(context, id)).Status);
        }
        Assert.True(await RetryAsync(context, id));
        var last = await AttemptAsync(context, id);
        Assert.Equal("failed", last.Status); Assert.Equal("service-unavailable", last.FailureReason); Assert.Equal(3, last.Submissions);
        Assert.Equal(3, server.Submissions.Count); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task ExpiredResultIsRequestedAgainFromANewJob()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context); Assert.True(await ExecuteAsync(context));
        server.Status = "completed"; server.ExpiredResult = true; var before = DateTimeOffset.UtcNow;
        Assert.True(await RetryAsync(context, id));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("queued", attempt.Status); Assert.Null(attempt.WhisperJobId); Assert.Equal(1, attempt.Submissions);
        AssertScheduledIn(attempt, TimeSpan.FromMinutes(5), before); Assert.Null(await ContentAsync(context, id)); await AssertCleanAsync(context, video);
        server.ExpiredResult = false; Assert.True(await RetryAsync(context, id)); Assert.True(await RetryAsync(context, id));
        Assert.Equal("ready", (await AttemptAsync(context, id)).Status); Assert.NotNull(await ContentAsync(context, id));
    }

    [Fact]
    public async Task JobThatTheServiceNoLongerKnowsFollowsTheResubmissionPolicy()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (_, id) = await EnqueuePreparedAsync(context); Assert.True(await ExecuteAsync(context)); server.JobExpired = true; var before = DateTimeOffset.UtcNow;
        Assert.True(await RetryAsync(context, id));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("queued", attempt.Status); Assert.Null(attempt.FailureReason); Assert.Null(attempt.WhisperJobId);
        AssertScheduledIn(attempt, TimeSpan.FromMinutes(5), before);
    }

    [Fact]
    public async Task UnknownResultSchemaFailsAsProcessingFailedWithoutAnotherSubmission()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context); Assert.True(await ExecuteAsync(context));
        server.Status = "completed"; server.Result = server.Result.Replace("\"schemaVersion\":1", "\"schemaVersion\":7", StringComparison.Ordinal);
        Assert.True(await RetryAsync(context, id));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("failed", attempt.Status); Assert.Equal("processing-failed", attempt.FailureReason);
        Assert.Null(await ContentAsync(context, id)); Assert.Single(server.Submissions); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task FailingExtractionIsRetriedAfterOneAndFiveMinutesThenFails()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server, includeAudio: false);
        var (video, id) = await EnqueuePreparedAsync(context);
        var delays = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5) };
        for (var extraction = 1; extraction <= 2; extraction++)
        {
            var before = DateTimeOffset.UtcNow; Assert.True(await RetryAsync(context, id));
            var attempt = await AttemptAsync(context, id);
            Assert.Equal("queued", attempt.Status); Assert.Equal(extraction, attempt.ExtractionAttempts);
            AssertScheduledIn(attempt, delays[extraction - 1], before); await AssertCleanAsync(context, video);
        }
        Assert.True(await RetryAsync(context, id));
        var last = await AttemptAsync(context, id);
        Assert.Equal("failed", last.Status); Assert.Equal("processing-failed", last.FailureReason); Assert.Equal(3, last.ExtractionAttempts);
        Assert.Empty(server.Submissions); await AssertCleanAsync(context, video);
        Assert.DoesNotContain(context.Logs.Entries, entry => entry.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VideoOverTwoHoursFailsLocallyWithDurationLimitAndNoRetry()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context);
        await context.WithScopeAsync(async provider =>
        {
            await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.videos SET duration_seconds = 7201 WHERE video_id = {video.VideoId}", Token); return true;
        });
        Assert.True(await ExecuteAsync(context));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("failed", attempt.Status); Assert.Equal("duration-limit", attempt.FailureReason);
        Assert.False(TranscriptionState.Derive("ready", [attempt], true).CanStart);
        Assert.Empty(server.Submissions); Assert.False(await ExecuteAsync(context)); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task OverdueDeadlineFailsAsTimedOutAndDoesNotAllowAutomaticRetry()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context); Assert.True(await ExecuteAsync(context));
        await context.WithScopeAsync(async provider =>
        {
            await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET deadline_at = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE transcription_id = {id}", Token); return true;
        });
        Assert.True(await RetryAsync(context, id));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("timed-out", attempt.FailureReason); Assert.True(TranscriptionState.Derive("ready", [attempt], true).CanStart);
        Assert.False(await ExecuteAsync(context)); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task UnreachableStatusKeepsWaitingInsteadOfFailingTheAttempt()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (_, id) = await EnqueuePreparedAsync(context); Assert.True(await ExecuteAsync(context));
        server.StatusStatusCode = 503; var before = DateTimeOffset.UtcNow;
        Assert.True(await RetryAsync(context, id));
        var attempt = await AttemptAsync(context, id);
        Assert.Equal("waiting", attempt.Status); Assert.Null(attempt.FailureReason); Assert.Equal("trn_test", attempt.WhisperJobId);
        Assert.True(attempt.NextActionAt > before);
        server.StatusStatusCode = 200; server.Status = "completed"; Assert.True(await RetryAsync(context, id));
        Assert.Equal("ready", (await AttemptAsync(context, id)).Status);
    }
}
