using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.Transcriptions.ReceiveTranscriptionEvent;
using CodeForCoders.Media.Application.UseCases.Videos.RequestVideoTranscription;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Transcriptions;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class MediaTranscriptionMetricsTests(VideoLibraryApiFactory factory)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory(DisplayName = nameof(CommittedCompletionRecordsOriginDurationAndReconciliationOnce))]
    [InlineData(false, "poll", "automatic")]
    [InlineData(true, "webhook", "automatic")]
    [InlineData(false, "poll", "requested")]
    [InlineData(true, "webhook", "requested")]
    [Trait("Layer", "Transcription metrics - Integration")]
    public async Task CommittedCompletionRecordsOriginDurationAndReconciliationOnce(bool signaled, string source, string origin)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await TranscriptionTestHarness.CreateContextAsync(factory, server);
        using var capture = new TranscriptionTelemetryCapture();
        var (_, id) = await TranscriptionTestHarness.EnqueuePreparedAsync(context);
        Assert.Equal("automatic", Tag(Assert.Single(capture.Values("media.transcription.requested")), "origin"));
        if (origin == "requested")
            await context.WithScopeAsync(async provider => await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE media_access.video_transcriptions SET origin = 'requested', requested_by_account_id = {Guid.CreateVersion7()} WHERE transcription_id = {id}", Token));
        Assert.True(await TranscriptionTestHarness.ExecuteAsync(context));
        server.Status = "completed";
        if (signaled)
            await ReceiveAsync(context, Input(id));
        else
            await TranscriptionTestHarness.MakeEligibleAsync(context, id);
        Assert.True(await TranscriptionTestHarness.ExecuteAsync(context));
        Assert.False(await TranscriptionTestHarness.ExecuteAsync(context));
        var attempt = await TranscriptionTestHarness.AttemptAsync(context, id);
        Assert.Equal("ready", attempt.Status);
        Assert.Equal("ready", Tag(Assert.Single(capture.Values("media.transcription.results")), "reason"));
        var latency = Assert.Single(capture.Values("media.transcription.time_to_ready"));
        Assert.Equal(origin, Tag(latency, "origin"));
        Assert.Equal((attempt.ReadyAt!.Value - attempt.RequestedAt).TotalSeconds, latency.Value, precision: 3);
        Assert.Equal(2, Assert.Single(capture.Values("media.transcription.duration")).Value);
        Assert.Equal(source, Tag(Assert.Single(capture.Values("media.transcription.reconciled")), "source"));
        AssertStages(capture, "extract-audio", "upload-audio", "submit", "poll", "fetch-result");
    }

    [Fact(DisplayName = nameof(ManualRequestCountsOnlyInsertedAttemptAndIgnoresReplay))]
    [Trait("Layer", "Transcription metrics - Integration")]
    public async Task ManualRequestCountsOnlyInsertedAttemptAndIgnoresReplay()
    {
        var video = Video.Create(Guid.CreateVersion7(), "private.teacher@example.test", Guid.CreateVersion7(), "Private teacher", DateTimeOffset.UtcNow);
        var lease = Guid.CreateVersion7();
        video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
        video.MarkReady(lease, 10, 1024, new byte[32], "test-key");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            db.Videos.Add(video);
            await db.SaveChangesAsync(Token);
        }
        using var capture = new TranscriptionTelemetryCapture();
        var actor = Guid.CreateVersion7();
        var key = Guid.CreateVersion7().ToString("D");
        foreach (var requestKey in new[] { key, key, Guid.CreateVersion7().ToString("D") })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(video.TenantId, actor);
            var output = await scope.ServiceProvider.GetRequiredService<IRequestVideoTranscription>()
                .ExecuteAsync(new(video.VideoId, requestKey), Token);
            Assert.Equal("in-progress", output.Transcription.Status);
        }
        Assert.Equal("requested", Tag(Assert.Single(capture.Values("media.transcription.requested")), "origin"));
        Assert.All(capture.Measurements, value => Assert.DoesNotContain(value.Tags, tag => tag.Value?.ToString() == actor.ToString("D")));
    }

    [Theory(DisplayName = nameof(TerminalFailureRecordsPersistedReasonWithoutReadySignals))]
    [InlineData("duration-limit")]
    [InlineData("size-limit")]
    [InlineData("service-unavailable")]
    [InlineData("processing-failed")]
    [InlineData("timed-out")]
    [Trait("Layer", "Transcription metrics - Integration")]
    public async Task TerminalFailureRecordsPersistedReasonWithoutReadySignals(string reason)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await TranscriptionTestHarness.CreateContextAsync(factory, server);
        var (video, id) = await TranscriptionTestHarness.EnqueuePreparedAsync(context);
        using var capture = new TranscriptionTelemetryCapture();
        if (reason == "duration-limit")
            await context.WithScopeAsync(async provider =>
                await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.videos SET duration_seconds = 7201 WHERE video_id = {video.VideoId}", Token));
        if (reason == "size-limit") server.SubmitStatusCode = 413;
        if (reason == "service-unavailable") server.SubmitStatusCode = 401;
        Assert.True(await TranscriptionTestHarness.ExecuteAsync(context));
        if (reason == "service-unavailable")
            for (var retry = 0; retry < 4; retry++) await TranscriptionTestHarness.RetryAsync(context, id);
        if (reason == "processing-failed")
        {
            server.Status = "failed";
            server.FailureCode = "PRIVATE_ERROR_private.teacher@example.test";
            await TranscriptionTestHarness.RetryAsync(context, id);
        }
        if (reason == "timed-out")
        {
            await context.WithScopeAsync(async provider => await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE media_access.video_transcriptions SET deadline_at = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE transcription_id = {id}", Token));
            await TranscriptionTestHarness.RetryAsync(context, id);
        }
        var attempt = await TranscriptionTestHarness.AttemptAsync(context, id);
        Assert.Equal("failed", attempt.Status);
        Assert.Equal(reason, attempt.FailureReason);
        Assert.Equal(reason, Tag(Assert.Single(capture.Values("media.transcription.results")), "reason"));
        Assert.Empty(capture.Values("media.transcription.time_to_ready"));
        Assert.Empty(capture.Values("media.transcription.duration"));
        if (reason == "service-unavailable") Assert.Equal(5, capture.Values("media.transcription.integration_rejected").Sum(value => value.Value));
        AssertTelemetryIsPrivate(capture, context, "private.teacher@example.test");
    }

    [Fact(DisplayName = nameof(TransientSubmissionFailureRecordsStageWithoutTerminalResult))]
    [Trait("Layer", "Transcription metrics - Integration")]
    public async Task TransientSubmissionFailureRecordsStageWithoutTerminalResult()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        server.SubmitStatusCode = 503;
        await using var context = await TranscriptionTestHarness.CreateContextAsync(factory, server);
        var (_, id) = await TranscriptionTestHarness.EnqueuePreparedAsync(context);
        using var capture = new TranscriptionTelemetryCapture();
        await TranscriptionTestHarness.ExecuteAsync(context);
        Assert.Equal("queued", (await TranscriptionTestHarness.AttemptAsync(context, id)).Status);
        Assert.Empty(capture.Values("media.transcription.results"));
        Assert.Empty(capture.Values("media.transcription.reconciled"));
        AssertStages(capture, "extract-audio", "upload-audio", "submit");
    }

    [Fact(DisplayName = nameof(QueueSnapshotsIncludeAllTenantsStatesAgesAndExplicitZeros))]
    [Trait("Layer", "Transcription metrics - Integration")]
    public async Task QueueSnapshotsIncludeAllTenantsStatesAgesAndExplicitZeros()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await TranscriptionTestHarness.CreateContextAsync(factory, server);
        var (_, id) = await TranscriptionTestHarness.EnqueuePreparedAsync(context);
        var (_, second) = await TranscriptionTestHarness.EnqueuePreparedAsync(context);
        await context.WithScopeAsync(async provider => await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE media_access.video_transcriptions SET requested_at = {DateTimeOffset.UtcNow.AddHours(-1)},
                next_action_at = {DateTimeOffset.UtcNow.AddHours(-1)} WHERE transcription_id IN ({id}, {second})
            """, Token));
        using var capture = new TranscriptionTelemetryCapture();
        await context.WithScopeAsync(async provider =>
        {
            provider.GetRequiredService<ITenantContext>().Set(Guid.CreateVersion7());
            using var worker = new MediaVolumeMetricsWorker(provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
                NullLogger<MediaVolumeMetricsWorker>.Instance, Options.Create(new OutboxOptions()));
            await worker.RefreshAsync(Token);
            capture.Observe();
            Assert.Equal(2, Assert.Single(capture.Values("media.transcription.pending"), value => Tag(value, "status") == "queued").Value);
            var age = Assert.Single(capture.Values("media.transcription.oldest_waiting"), value => Tag(value, "status") == "queued");
            Assert.InRange(age.Value, 3595, 3620);
            Assert.Equal(2, Assert.Single(capture.Values("media.transcription.stuck")).Value);
            Assert.Equal(3, capture.Values("media.transcription.pending").Length);
            foreach (var status in new[] { "working", "waiting" })
            {
                await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE media_access.video_transcriptions SET status = {status}, lease_until = {DateTimeOffset.UtcNow.AddMinutes(5)},
                        next_action_at = {DateTimeOffset.UtcNow.AddMinutes(5)} WHERE transcription_id = {second}
                    """, Token);
                capture.Measurements.Clear();
                await worker.RefreshAsync(Token);
                capture.Observe();
                Assert.Equal(1, Assert.Single(capture.Values("media.transcription.pending"), value => Tag(value, "status") == "queued").Value);
                Assert.Equal(1, Assert.Single(capture.Values("media.transcription.pending"), value => Tag(value, "status") == status).Value);
                Assert.InRange(Assert.Single(capture.Values("media.transcription.oldest_waiting"), value => Tag(value, "status") == status).Value, 3595, 3620);
                Assert.Equal(1, Assert.Single(capture.Values("media.transcription.stuck")).Value);
            }
            await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE media_access.video_transcriptions SET status = 'failed', failure_reason = 'processing-failed' WHERE transcription_id IN ({id}, {second})", Token);
            capture.Measurements.Clear();
            await worker.RefreshAsync(Token);
            capture.Observe();
            Assert.Equal(3, capture.Values("media.transcription.pending").Length);
            Assert.All(capture.Measurements, measurement => Assert.Equal(0, measurement.Value));
            return true;
        });
    }

    [Fact(DisplayName = nameof(WebhookRecordsSignaledDuplicateIgnoredAndInvalidOutcomesWithoutPayload))]
    [Trait("Layer", "Transcription metrics - Integration")]
    public async Task WebhookRecordsSignaledDuplicateIgnoredAndInvalidOutcomesWithoutPayload()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await TranscriptionTestHarness.CreateContextAsync(factory, server);
        var (_, id) = await TranscriptionTestHarness.EnqueuePreparedAsync(context);
        await TranscriptionTestHarness.ExecuteAsync(context);
        using var capture = new TranscriptionTelemetryCapture();
        var input = Input(id);
        await ReceiveAsync(context, input);
        await ReceiveAsync(context, input);
        await ReceiveAsync(context, Input(Guid.CreateVersion7()));
        await Assert.ThrowsAsync<MediaApiException>(() => ReceiveAsync(context, input with { Signature = "v1,invalid" }));
        await Assert.ThrowsAsync<MediaApiException>(() => ReceiveAsync(context, Input(id, "queued")));
        await context.WithScopeAsync(async provider =>
        {
            var receiver = new ReceiveTranscriptionEvent(CreateSignature(), new UnavailableEventRepository(),
                provider.GetRequiredService<IUnitOfWork>(), TimeProvider.System);
            await Assert.ThrowsAsync<MediaApiException>(() => receiver.ExecuteAsync(Input(id), Token));
            return true;
        });
        var outcomes = capture.Values("media.transcription.webhook_received").Select(value => Tag(value, "outcome")).Order(StringComparer.Ordinal);
        Assert.Equal(new[] { "duplicate", "ignored", "invalid-request", "signaled", "signature-invalid", "storage-unavailable" }, outcomes);
        Assert.Equal(6, capture.Values("media.transcription.stage_duration").Length);
        Assert.All(capture.Activities, activity => Assert.Equal("media.transcription.webhook.receive", activity.OperationName));
        AssertTelemetryIsPrivate(capture, context, input.Signature);
    }

    [Fact(DisplayName = nameof(AllStageAttributesMetricsAndLogsExcludeTextSignedUrlsAndCredentials))]
    [Trait("Layer", "Transcription metrics - Integration")]
    public async Task AllStageAttributesMetricsAndLogsExcludeTextSignedUrlsAndCredentials()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        server.Result = server.Result.Replace("private lesson", "private.teacher@example.test", StringComparison.Ordinal);
        await using var context = await TranscriptionTestHarness.CreateContextAsync(factory, server);
        var (video, id) = await TranscriptionTestHarness.EnqueuePreparedAsync(context);
        using var capture = new TranscriptionTelemetryCapture();
        await TranscriptionTestHarness.ExecuteAsync(context);
        await ReceiveAsync(context, Input(id));
        server.Status = "completed";
        await TranscriptionTestHarness.ExecuteAsync(context);
        AssertStages(capture, "extract-audio", "upload-audio", "submit", "poll", "fetch-result", "webhook.receive");
        Assert.All(capture.Activities, activity =>
        {
            Assert.Equal(video.VideoId.ToString("D"), activity.GetTagItem("video.id"));
            Assert.Equal(id.ToString("D"), activity.GetTagItem("transcription.id"));
        });
        AssertTelemetryIsPrivate(capture, context, Assert.Single(server.Submissions).SourceUrl, "private.teacher@example.test", "trn_test");
    }

    private static void AssertStages(TranscriptionTelemetryCapture capture, params string[] stages)
    {
        var durations = capture.Values("media.transcription.stage_duration");
        foreach (var stage in stages)
        {
            Assert.Contains(durations, value => Tag(value, "stage") == stage);
            Assert.Contains(capture.Activities, activity => activity.OperationName == "media.transcription." + stage);
        }
        Assert.All(durations, value => Assert.True(value.Value >= 0));
    }

    private static void AssertTelemetryIsPrivate(TranscriptionTelemetryCapture capture, VideoPreparationTestContext context, params string[] secrets)
    {
        var allowedDimensions = new[] { "origin", "reason", "source", "outcome", "stage", "status" };
        Assert.All(capture.Measurements, value => Assert.All(value.Tags, tag => Assert.Contains(tag.Key, allowedDimensions)));
        Assert.All(capture.Activities, activity => Assert.All(activity.TagObjects, tag => Assert.Contains(tag.Key, new[] { "video.id", "transcription.id" })));
        var emitted = string.Join(" ", context.Logs.Entries.Concat(capture.Measurements.Select(value => string.Join(" ", value.Tags)))
            .Concat(capture.Activities.Select(activity => activity.OperationName + " " + string.Join(" ", activity.TagObjects))));
        foreach (var secret in secrets.Concat(new[] { "X-Amz-", "private lesson", TranscriptionHttpSimulator.ApiKey }))
            Assert.DoesNotContain(secret, emitted, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Tag(TranscriptionTelemetryCapture.MetricValue value, string name) => value.Tags.Single(tag => tag.Key == name).Value?.ToString();
    private static readonly byte[] WebhookKey = RandomNumberGenerator.GetBytes(24);

    private static ReceiveTranscriptionEventInput Input(Guid id, string status = "completed")
    {
        var eventId = "evt_" + Guid.CreateVersion7().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var text = JsonSerializer.Serialize(new { eventId, jobId = "trn_test", clientReference = id.ToString("D"), status });
        var signature = "v1," + Convert.ToBase64String(HMACSHA256.HashData(WebhookKey, Encoding.UTF8.GetBytes($"{eventId}.{timestamp}.{text}")));
        return new(Encoding.UTF8.GetBytes(text), eventId, timestamp, signature);
    }

    private static Task<bool> ReceiveAsync(VideoPreparationTestContext context, ReceiveTranscriptionEventInput input)
        => context.WithScopeAsync(async provider =>
        {
            var signature = new TranscriptionWebhookSignature(Options.Create(new TranscriptionWebhookOptions
            { Secrets = ["whsec_" + Convert.ToBase64String(WebhookKey)] }), TimeProvider.System);
            var receiver = new ReceiveTranscriptionEvent(signature, new TranscriptionEventRepository(provider.GetRequiredService<MediaDbContext>()),
                provider.GetRequiredService<IUnitOfWork>(), TimeProvider.System);
            await receiver.ExecuteAsync(input, Token);
            return true;
        });

    private static TranscriptionWebhookSignature CreateSignature()
        => new(Options.Create(new TranscriptionWebhookOptions { Secrets = ["whsec_" + Convert.ToBase64String(WebhookKey)] }), TimeProvider.System);

    private sealed class UnavailableEventRepository : ITranscriptionEventRepository
    {
        public Task<bool> RecordAsync(TranscriptionNotification notification, Func<Transcription?, CancellationToken, Task> change, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }

}
