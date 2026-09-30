using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(MediaIntegrationCollection.Name)]
public sealed class MediaVolumeMetricsTests(MediaIntegrationFixture fixture)
{
    [Fact]
    public async Task StorageAndStatusesMatchPostgreSql()
    {
        await ResetAsync();
        await SeedScenarioAsync();
        var values = await CollectAsync();
        Assert.Equal(30 * 1024 * 1024, values.Single(value => value.Name == "media.storage.used").Value);
        Assert.Equal(2, values.Single(value => value.Name == "media.videos.count" && value.Status == "ready").Value);
        Assert.Equal(1, values.Single(value => value.Name == "media.videos.count" && value.Status == "failed").Value);
        Assert.Equal(1, values.Single(value => value.Name == "media.videos.count" && value.Status == "received").Value);
        Assert.Equal(0, values.Single(value => value.Name == "media.videos.count" && value.Status == "preparing").Value);
        Assert.Equal(1, values.Single(value => value.Name == "media.videos.stuck").Value);
    }

    [Fact]
    public async Task OldReceivedVideoIsStuckWithoutDuration()
    {
        await ResetAsync();
        await SeedAsync(Video.Create(Guid.CreateVersion7(), "Old", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow.AddHours(-13)));
        var values = await CollectAsync();
        Assert.Equal(1, values.Single(value => value.Name == "media.videos.stuck").Value);
    }

    [Fact]
    public async Task OldPreparingVideoIsStuckWithoutDuration()
    {
        await ResetAsync();
        var old = Video.Create(Guid.CreateVersion7(), "Old preparation", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow.AddHours(-13));
        var recent = Video.Create(Guid.CreateVersion7(), "Recent preparation", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow.AddHours(-1));
        old.MarkPreparing(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(5));
        recent.MarkPreparing(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(5));
        await SeedAsync(old, recent);

        var values = await CollectAsync();
        Assert.Equal(1, values.Single(value => value.Name == "media.videos.stuck").Value);
    }

    [Fact]
    public async Task DurationControlsStuckThreshold()
    {
        await ResetAsync();
        var old = Video.Create(Guid.CreateVersion7(), "Past estimate", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow.AddMinutes(-5));
        var recent = Video.Create(Guid.CreateVersion7(), "Within estimate", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow.AddMinutes(-2));
        await SeedAsync(old, recent);
        await using (var db = CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.videos SET duration_seconds = 60 WHERE video_id IN ({old.VideoId}, {recent.VideoId})", TestContext.Current.CancellationToken);
        }

        var values = await CollectAsync();
        Assert.Equal(1, values.Single(value => value.Name == "media.videos.stuck").Value);
    }

    [Fact]
    public async Task InstrumentsContainOnlyStatusDimension()
    {
        await ResetAsync();
        await SeedAsync(Video.Create(Guid.CreateVersion7(), "Private", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow));
        var values = await CollectAsync();
        var names = values.Select(value => value.Name).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            [
                "media.outbox.exhausted",
                "media.outbox.pending",
                "media.storage.used",
                "media.uploads.pending",
                "media.videos.count",
                "media.videos.stuck",
            ],
            names);
        Assert.All(values, value => Assert.All(value.Tags, tag => Assert.Equal("status", tag.Key)));
        Assert.All(
            values.Where(value => value.Name == "media.videos.count"),
            value => Assert.Equal("status", Assert.Single(value.Tags).Key));
        Assert.All(
            values.Where(value => value.Name != "media.videos.count"),
            value => Assert.Empty(value.Tags));
    }

    [Fact]
    public async Task InvalidDataContextDoesNotStopMetricsWorker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.Configure<OutboxOptions>(_ => { });
        services.AddSingleton<MediaVolumeMetricsWorker>();
        await using var provider = services.BuildServiceProvider();
        var worker = provider.GetRequiredService<MediaVolumeMetricsWorker>();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(worker.ExecuteTask);
        Assert.False(worker.ExecuteTask.IsCompleted);
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ApiRoleDoesNotRegisterMetricsWorker()
    {
        static IServiceCollection ServicesFor(string role)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Media:Role"] = role })
                .Build();
            return new ServiceCollection().AddMessagingConfiguration(configuration);
        }

        Assert.DoesNotContain(ServicesFor("api"), descriptor =>
            descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(MediaVolumeMetricsWorker));
        Assert.Contains(ServicesFor("worker"), descriptor =>
            descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(MediaVolumeMetricsWorker));
    }

    private async Task<List<MetricValue>> CollectAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.Configure<OutboxOptions>(_ => { });
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(fixture.PostgreSql.GetConnectionString()));
        services.AddSingleton<MediaVolumeMetricsWorker>();
        await using var provider = services.BuildServiceProvider();
        using var listener = new MeterListener();
        var values = new List<MetricValue>();
        var capture = false;
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (capture && instrument.Meter == MediaTelemetry.Meter && instrument.Name.StartsWith("media.", StringComparison.Ordinal))
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            values.Add(new MetricValue(instrument.Name, value, tags.ToArray())));
        listener.Start();
        capture = true;
        var worker = provider.GetRequiredService<MediaVolumeMetricsWorker>();
        await worker.RefreshAsync(TestContext.Current.CancellationToken);
        listener.RecordObservableInstruments();
        return values;
    }

    private async Task SeedScenarioAsync()
    {
        var tenant = Guid.CreateVersion7();
        var ready10 = Video.Create(tenant, "Ten", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        var ready20 = Video.Create(tenant, "Twenty", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        var failed = Video.Create(tenant, "Failed", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        var received = Video.Create(tenant, "Received", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow.AddHours(-13));
        Ready(ready10, 10 * 1024 * 1024);
        Ready(ready20, 20 * 1024 * 1024);
        var lease = Guid.CreateVersion7();
        failed.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
        failed.MarkFailed(lease, VideoFailureReasons.UnreadableFile);
        await SeedAsync(ready10, ready20, failed, received);
    }

    private static void Ready(Video video, long bytes)
    {
        var lease = Guid.CreateVersion7();
        video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
        video.MarkReady(lease, 60, bytes, new byte[29], "test-key");
    }

    private async Task SeedAsync(params Video[] videos)
    {
        await using var db = CreateDbContext();
        db.Videos.AddRange(videos);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE media_access.videos CASCADE", TestContext.Current.CancellationToken);
    }

    private MediaDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<MediaDbContext>().UseNpgsql(fixture.PostgreSql.GetConnectionString()).Options, new TenantContext());

    private sealed record MetricValue(string Name, long Value, KeyValuePair<string, object?>[] Tags)
    {
        public string? Status => Tags.SingleOrDefault(tag => tag.Key == "status").Value as string;
    }
}
