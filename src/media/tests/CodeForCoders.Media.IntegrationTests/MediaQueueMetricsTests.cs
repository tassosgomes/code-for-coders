using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class MediaQueueMetricsTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(OldestWaitingReportsOldestReceivedAndExplicitZeroWhenEmpty))]
    [Trait("Layer", "Media queue metrics - Integration")]
    public async Task OldestWaitingReportsOldestReceivedAndExplicitZeroWhenEmpty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = CreateMetricsServices();
        await ResetAsync(services, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var oldest = Video.Create(Guid.CreateVersion7(), "Oldest", Guid.CreateVersion7(), "Teacher", now.AddHours(-2));
        var newer = Video.Create(Guid.CreateVersion7(), "Newer", Guid.CreateVersion7(), "Teacher", now.AddMinutes(-10));
        var preparing = Video.Create(Guid.CreateVersion7(), "Preparing", Guid.CreateVersion7(), "Teacher", now.AddHours(-24));
        preparing.MarkPreparing(Guid.CreateVersion7(), now.AddHours(1));
        await SeedAsync(services, [oldest, newer, preparing], cancellationToken);

        using var metrics = new MetricCapture();
        var worker = services.GetRequiredService<MediaVolumeMetricsWorker>();
        await worker.RefreshAsync(cancellationToken);
        var age = Assert.Single(metrics.Observe("media.videos.oldest_waiting"));
        Assert.InRange(age.Value, 7200, 7220);
        Assert.Empty(age.Tags);

        await ResetAsync(services, cancellationToken);
        await worker.RefreshAsync(cancellationToken);
        var emptyQueue = metrics.Observe("media.videos.oldest_waiting");
        Assert.Equal(2, emptyQueue.Length);
        Assert.Equal(0, emptyQueue[^1].Value);
        Assert.Empty(emptyQueue[^1].Tags);
    }

    [Fact(DisplayName = nameof(ClaimIncrementsClaimedCounter))]
    [Trait("Layer", "Media queue metrics - Integration")]
    public async Task ClaimIncrementsClaimedCounter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(StorageDownloadFailures: 1));
        await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);

        using var metrics = new MetricCapture();
        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        Assert.Equal(1, metrics.CounterValue("media.videos.claimed"));
        Assert.Empty(Assert.Single(metrics.Measurements("media.videos.claimed")).Tags);
    }

    [Fact(DisplayName = nameof(ClaimRecordsWaitFromUploadedAt))]
    [Trait("Layer", "Media queue metrics - Integration")]
    public async Task ClaimRecordsWaitFromUploadedAt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(StorageDownloadFailures: 1));
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);
        var uploadedAt = DateTimeOffset.UtcNow.AddSeconds(-120);
        await context.WithScopeAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MediaDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE media_access.videos SET uploaded_at = {uploadedAt} WHERE video_id = {video.VideoId}",
                cancellationToken);
            return true;
        });

        using var metrics = new MetricCapture();
        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var wait = Assert.Single(metrics.Measurements("media.videos.wait"));
        Assert.InRange(wait.Value, 119, 130);
        Assert.Empty(wait.Tags);
    }

    [Fact(DisplayName = nameof(RecollectIncrementsRetriedCounter))]
    [Trait("Layer", "Media queue metrics - Integration")]
    public async Task RecollectIncrementsRetriedCounter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(StorageDownloadFailures: 2));
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);

        using var metrics = new MetricCapture();
        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));
        await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        Assert.Equal(1, metrics.CounterValue("media.videos.retried"));
        Assert.Equal(2, metrics.CounterValue("media.videos.claimed"));
        Assert.Empty(Assert.Single(metrics.Measurements("media.videos.retried")).Tags);
    }

    private ServiceProvider CreateMetricsServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.Configure<OutboxOptions>(_ => { });
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(factory.VideoPreparationConnectionString));
        services.AddSingleton<MediaVolumeMetricsWorker>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task ResetAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE media_access.videos CASCADE",
            cancellationToken);
    }

    private static async Task SeedAsync(
        IServiceProvider services,
        Video[] videos,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        dbContext.Videos.AddRange(videos);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class MetricCapture : IDisposable
    {
        private static readonly HashSet<string> InstrumentNames =
        [
            "media.videos.claimed",
            "media.videos.retried",
            "media.videos.wait",
            "media.videos.oldest_waiting",
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

        public MetricValue[] Observe(string name)
        {
            Listener.RecordObservableInstruments();
            return Measurements(name);
        }

        public long CounterValue(string name)
            => checked((long)Measurements(name).Sum(measurement => measurement.Value));

        public void Dispose()
            => Listener.Dispose();
    }

    private sealed record MetricValue(string Name, double Value, KeyValuePair<string, object?>[] Tags);
}
