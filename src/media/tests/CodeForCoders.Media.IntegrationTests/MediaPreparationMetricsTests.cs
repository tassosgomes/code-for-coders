using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class MediaPreparationMetricsTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(ReadyPreparationRecordsOutcomeDurationsAndVideoTags))]
    [Trait("Layer", "Media preparation metrics - Integration")]
    public async Task ReadyPreparationRecordsOutcomeDurationsAndVideoTags()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoAsync(320, 240, 1, cancellationToken);
        using var metrics = new MetricCapture();
        using var activities = new ActivityCapture();

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("ready", stored.Status);
        Assert.Equal(1, metrics.CounterValue("media.videos.completed"));
        var timeToReady = Assert.Single(metrics.Measurements("media.videos.time_to_ready"));
        Assert.True(timeToReady.Value >= 0);
        Assert.Empty(timeToReady.Tags);

        var durations = metrics.Measurements("media.videos.prepare_duration");
        Assert.Equal(4, durations.Length);
        Assert.Equal(
            new[] { "download", "probe", "publish", "transcode" },
            durations.Select(measurement => TagValue(measurement, "stage")).Order(StringComparer.Ordinal));
        Assert.All(durations, measurement => Assert.True(measurement.Value >= 0));
        AssertVideoActivityTags(activities, video.VideoId);
    }

    [Fact(DisplayName = nameof(UnreadableVideoCountsPersistedReasonWithoutCompletion))]
    [Trait("Layer", "Media preparation metrics - Integration")]
    public async Task UnreadableVideoCountsPersistedReasonWithoutCompletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoWithBytesAsync([0x00, 0x7f, 0x00, 0x01, 0xff], cancellationToken);
        using var metrics = new MetricCapture();
        using var activities = new ActivityCapture();

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("failed", stored.Status);
        Assert.Equal(VideoFailureReasons.UnreadableFile, stored.FailureReason);
        var failure = Assert.Single(metrics.Measurements("media.videos.failed"));
        Assert.Equal("unreadable-file", TagValue(failure, "reason"));
        Assert.Equal(0, metrics.CounterValue("media.videos.completed"));
        Assert.Empty(metrics.Measurements("media.videos.time_to_ready"));
        AssertVideoActivityTags(activities, video.VideoId);
    }

    [Fact(DisplayName = nameof(TransientFailureThatBecomesReadyIsOnlyCountedAsRetried))]
    [Trait("Layer", "Media preparation metrics - Integration")]
    public async Task TransientFailureThatBecomesReadyIsOnlyCountedAsRetried()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(StorageDownloadFailures: 1));
        var video = await context.CreateQueuedVideoAsync(320, 240, 1, cancellationToken);
        using var metrics = new MetricCapture();

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));
        Assert.Equal("received", (await ReadVideoAsync(context, video.VideoId, cancellationToken)).Status);
        Assert.Empty(metrics.Measurements("media.videos.failed"));
        Assert.Equal(0, metrics.CounterValue("media.videos.completed"));

        await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        Assert.Equal("ready", (await ReadVideoAsync(context, video.VideoId, cancellationToken)).Status);
        Assert.Equal(1, metrics.CounterValue("media.videos.retried"));
        Assert.Empty(metrics.Measurements("media.videos.failed"));
        Assert.Equal(1, metrics.CounterValue("media.videos.completed"));
        Assert.Single(metrics.Measurements("media.videos.time_to_ready"));
    }

    [Fact(DisplayName = nameof(UnsupportedVideoCountsUnsupportedFormatFromPersistedReason))]
    [Trait("Layer", "Media preparation metrics - Integration")]
    public async Task UnsupportedVideoCountsUnsupportedFormatFromPersistedReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoWithUnknownCodecAsync(cancellationToken);
        using var metrics = new MetricCapture();

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("failed", stored.Status);
        Assert.Equal(VideoFailureReasons.UnsupportedFormat, stored.FailureReason);
        Assert.Equal("unsupported-format", TagValue(
            Assert.Single(metrics.Measurements("media.videos.failed")),
            "reason"));
        Assert.Equal(0, metrics.CounterValue("media.videos.completed"));
    }

    [Fact(DisplayName = nameof(UnexpectedFailuresCountAttemptsExhaustedOnlyAfterFinalCommit))]
    [Trait("Layer", "Media preparation metrics - Integration")]
    public async Task UnexpectedFailuresCountAttemptsExhaustedOnlyAfterFinalCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(
                TranscoderFfmpegPath: Path.Combine(Path.GetTempPath(), $"missing-ffmpeg-{Guid.CreateVersion7():N}")));
        var video = await context.CreateQueuedVideoAsync(320, 240, 1, cancellationToken);
        using var metrics = new MetricCapture();
        using var activities = new ActivityCapture();

        for (var attempt = 1; attempt <= Video.MaximumPreparationAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
            }

            Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));
            var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
            Assert.Equal(attempt == Video.MaximumPreparationAttempts ? "failed" : "received", stored.Status);
            Assert.Equal(attempt == Video.MaximumPreparationAttempts ? VideoFailureReasons.PreparationFailed : null, stored.FailureReason);
            Assert.Equal(attempt == Video.MaximumPreparationAttempts ? 1 : 0, metrics.Measurements("media.videos.failed").Length);
        }

        Assert.Equal(1, metrics.CounterValue("media.videos.failed"));
        Assert.Equal("attempts-exhausted", TagValue(
            Assert.Single(metrics.Measurements("media.videos.failed")),
            "reason"));
        Assert.Equal(0, metrics.CounterValue("media.videos.completed"));
        Assert.Equal(Video.MaximumPreparationAttempts - 1, metrics.CounterValue("media.videos.retried"));
        AssertVideoActivityTags(activities, video.VideoId);
    }

    private static void AssertVideoActivityTags(ActivityCapture activities, Guid videoId)
    {
        var videoActivities = activities.Activities
            .Where(activity => activity.Name.StartsWith("media.video.", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(videoActivities);
        Assert.All(videoActivities, activity =>
            Assert.Equal(videoId.ToString("D"), activity.Tags.Single(tag => tag.Key == "video.id").Value));
    }

    private static string? TagValue(MetricValue measurement, string name)
        => measurement.Tags.SingleOrDefault(tag => tag.Key == name).Value?.ToString();

    private static async Task<StoredVideo> ReadVideoAsync(
        VideoPreparationTestContext context,
        Guid videoId,
        CancellationToken cancellationToken)
        => await context.WithScopeAsync(async services => await services.GetRequiredService<MediaDbContext>()
            .Videos.IgnoreQueryFilters().AsNoTracking()
            .Where(video => video.VideoId == videoId)
            .Select(video => new StoredVideo(video.Status, video.FailureReason))
            .SingleAsync(cancellationToken));

    private sealed class MetricCapture : IDisposable
    {
        private static readonly HashSet<string> InstrumentNames =
        [
            "media.videos.completed",
            "media.videos.failed",
            "media.videos.prepare_duration",
            "media.videos.retried",
            "media.videos.time_to_ready",
        ];

        private readonly ConcurrentQueue<MetricValue> measurements = new();

        public MetricCapture()
        {
            Listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == MediaTelemetry.Meter && InstrumentNames.Contains(instrument.Name))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            Listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                measurements.Enqueue(new MetricValue(instrument.Name, value, tags.ToArray())));
            Listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                measurements.Enqueue(new MetricValue(instrument.Name, value, tags.ToArray())));
            Listener.Start();
        }

        private MeterListener Listener { get; } = new();

        public MetricValue[] Measurements(string name)
            => measurements.Where(measurement => measurement.Name == name).ToArray();

        public long CounterValue(string name)
            => checked((long)Measurements(name).Sum(measurement => measurement.Value));

        public void Dispose()
            => Listener.Dispose();
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ConcurrentQueue<ActivityValue> activities = new();

        public ActivityCapture()
        {
            Listener.ShouldListenTo = source => source.Name == MediaTelemetry.ActivitySourceName;
            Listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            Listener.ActivityStopped = activity => activities.Enqueue(new ActivityValue(activity.OperationName, activity.Tags.ToArray()));
            ActivitySource.AddActivityListener(Listener);
        }

        private ActivityListener Listener { get; } = new();

        public ActivityValue[] Activities => activities.ToArray();

        public void Dispose()
            => Listener.Dispose();
    }

    private sealed record MetricValue(string Name, double Value, KeyValuePair<string, object?>[] Tags);

    private sealed record ActivityValue(string Name, KeyValuePair<string, string?>[] Tags);

    private sealed record StoredVideo(string Status, string? FailureReason);
}
